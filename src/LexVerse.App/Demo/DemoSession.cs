using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using LexVerse.Application.Realtime;
using LexVerse.Core.Capture;
using LexVerse.Core.Geometry;

namespace LexVerse.App.Demo;

internal sealed class DemoSession : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _pipe = $"lv-recording-{Guid.NewGuid():N}";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _watch;
    private Process? _sample;
    private bool _polling;
    private bool _disposed;
    private bool _ready;
    public string CaseId { get; }
    public RealtimeCoordinatorState State { get; private set; } = RealtimeCoordinatorState.Stopped;
    public event EventHandler? Changed;

    public bool IsSampleWindow(WindowCaptureTarget target) =>
        _sample is { HasExited: false } && target.ProcessId == (uint)_sample.Id;

    public DemoSession(string caseId)
    {
        if (caseId is not ("article" or "game" or "letters" or "comic"))
            throw new ArgumentException("Choose article, game, letters or comic.");
        CaseId = caseId;
        _watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _watch.Tick += async (_, _) =>
        {
            if (_polling || _disposed || !_ready) return;
            _polling = true;
            try
            {
                if (_sample?.HasExited != false) { SetStopped(); _watch.Stop(); return; }
                if (State.Status == RealtimeCoordinatorStatus.Running) await SendAsync(new { operation = "refresh" });
            }
            catch (Exception) { if (!_disposed) SetStopped(); }
            finally { _polling = false; }
        };
    }

    public async Task OpenAsync()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the executable.");
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = false };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(typeof(App).Assembly.Location);
        foreach (var value in new[] { "--sample-app", "--case", CaseId, "--pipe", _pipe, "--parent", Environment.ProcessId.ToString() })
            info.ArgumentList.Add(value);
        _sample = Process.Start(info) ?? throw new InvalidOperationException("Cannot open the sample application.");
        await SendAsync(new { operation = "ping" }, TimeSpan.FromSeconds(25));
        _ready = true;
        _watch.Start();
    }

    public async Task StartAsync(RealtimeCaptureKind kind, ScreenRect bounds, string source, string target,
        object style, WindowCaptureTarget? window = null)
    {
        EnsurePair(source, target);
        if (kind == RealtimeCaptureKind.Window && (window is null || !IsSampleWindow(window)))
            throw new InvalidOperationException("Hãy chọn cửa sổ nội dung của ca quay đang mở.");
        try { await SendAsync(new { operation = "start", kind = kind.ToString().ToLowerInvariant(), bounds, style }); }
        catch { SetStopped(); throw; }
        FocusSample();
        State = new(RealtimeCoordinatorStatus.Running, kind);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string?> PopupAsync(string source, string target, object style)
    {
        EnsurePair(source, target);
        var result = await SendAsync(new { operation = "popup", style });
        FocusSample();
        return result.TryGetProperty("text", out var text) ? text.GetString() : null;
    }

    public async Task<string?> QuickAsync(string text, string source, string target)
    {
        EnsurePair(source, target);
        var result = await SendAsync(new { operation = "quick", text });
        return result.TryGetProperty("text", out var translated) ? translated.GetString() : null;
    }

    public async Task StopAsync(bool clear = false)
    {
        if (_ready && _sample?.HasExited == false)
            await SendAsync(new { operation = clear ? "clear" : "stop" });
        SetStopped();
    }

    private void EnsurePair(string source, string target)
    {
        var expected = CaseId switch { "game" => "ja", "letters" => "ko", "comic" => "zh", _ => "en" };
        if ((source != "auto" && source != expected) || target != (CaseId == "game" ? "en" : "vi"))
            throw new InvalidOperationException("Cặp ngôn ngữ này chưa có dữ liệu trong ca quay đang mở.");
    }

    private void SetStopped()
    {
        State = RealtimeCoordinatorState.Stopped;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void FocusSample()
    {
        if (_sample?.HasExited != false) return;
        _sample.Refresh();
        var handle = _sample.MainWindowHandle;
        if (handle == IntPtr.Zero) return;
        if (IsIconic(handle)) ShowWindow(handle, 9);
        SetForegroundWindow(handle);
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);

    private async Task<JsonElement> SendAsync(object request, TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        cancellation.CancelAfter(timeout ?? TimeSpan.FromSeconds(4));
        var token = cancellation.Token;
        await _gate.WaitAsync(token);
        try
        {
            using var pipe = new NamedPipeClientStream(".", _pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(token);
            using var reader = new StreamReader(pipe, leaveOpen: true);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), token);
            var line = await reader.ReadLineAsync(token) ?? throw new IOException("The sample window disconnected.");
            var result = JsonSerializer.Deserialize<JsonElement>(line, Json);
            if (result.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetString());
            return result;
        }
        catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
        {
            throw new InvalidOperationException("Chưa kết nối được cửa sổ nội dung. Hãy mở lại ca quay.");
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watch.Stop();
        _lifetime.Cancel();
        if (_sample is { HasExited: false }) _sample.CloseMainWindow();
        _sample?.Dispose();
        _lifetime.Dispose();
    }
}
