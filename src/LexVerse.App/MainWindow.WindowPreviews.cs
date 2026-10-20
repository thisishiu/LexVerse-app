using LexVerse.Application.Desktop;
using LexVerse.Core.Capture;

namespace LexVerse.App;

public partial class MainWindow
{
    private sealed record WindowPreviewsPayload(string SessionId, string[] WindowIds);
    private sealed record CancelWindowPreviewsPayload(string SessionId);
    private CancellationTokenSource? _windowPreviewCancellation;
    private string? _windowPreviewSessionId;
    private readonly SemaphoreSlim _windowPreviewSlots = new(2, 2);
    private readonly SemaphoreSlim _windowPreviewBatch = new(1, 1);

    private void CancelWindowPreviews()
    {
        _windowPreviewCancellation?.Cancel();
        _windowPreviewCancellation?.Dispose();
        _windowPreviewCancellation = null;
        _windowPreviewSessionId = null;
    }

    // Preview commands bypass the translation command gate, so cancellation is immediate.
    private async Task HandleWindowPreviewsAsync(DesktopBridgeCommand command, CancellationToken lifetime)
    {
        try
        {
            if (command.Type == "realtime.cancelWindowPreviews")
            {
                var cancel = DesktopBridgeProtocol.DeserializePayload<CancelWindowPreviewsPayload>(command.Payload);
                if (cancel.SessionId == _windowPreviewSessionId) CancelWindowPreviews();
                PostDesktopResult(command.Id, true);
                return;
            }
            var payload = DesktopBridgeProtocol.DeserializePayload<WindowPreviewsPayload>(command.Payload);
            if (string.IsNullOrWhiteSpace(payload.SessionId) || payload.SessionId.Length > 128 || payload.WindowIds is null || payload.WindowIds.Length > 12)
                throw new DesktopBridgePayloadException("invalid-payload");
            if (payload.SessionId != _windowPreviewSessionId)
            {
                CancelWindowPreviews();
                _windowPreviewSessionId = payload.SessionId;
                _windowPreviewCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            }
            var token = _windowPreviewCancellation!.Token;
            // Resolve on the UI thread; workers never touch the registry dictionary.
            var targets = payload.WindowIds.Distinct().Select(id =>
            {
                WindowCaptureTarget? target;
                try { target = _windowTargets.Resolve(id); }
                catch (InvalidOperationException) { target = null; }
                return (Id: id, Target: target);
            }).ToArray();
            await _windowPreviewBatch.WaitAsync(token);
            try
            {
                var result = await Task.WhenAll(targets.Select(async item =>
                {
                    if (item.Target is null) return new WindowPreview(item.Id, "closed");
                    await _windowPreviewSlots.WaitAsync(token);
                    try { return await Task.Run(() => WindowPreviewCapture.CaptureAsync(item.Id, item.Target, token), token); }
                    finally { _windowPreviewSlots.Release(); }
                }));
                token.ThrowIfCancellationRequested();
                PostDesktopResult(command.Id, true, result);
            }
            finally { _windowPreviewBatch.Release(); }
        }
        catch (DesktopBridgePayloadException exception) { PostDesktopResult(command.Id, false, errorCode: exception.ErrorCode); }
        catch (System.Text.Json.JsonException) { PostDesktopResult(command.Id, false, errorCode: "invalid-payload"); }
        catch (OperationCanceledException) { PostDesktopResult(command.Id, false, errorCode: "operation-cancelled"); }
        catch { PostDesktopResult(command.Id, false, errorCode: "operation-failed"); }
    }
}
