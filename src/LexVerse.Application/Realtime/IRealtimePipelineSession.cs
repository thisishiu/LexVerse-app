namespace LexVerse.Application.Realtime;

public interface IRealtimePipelineSession : IAsyncDisposable
{
    /// <summary>Allows target lifecycle checks during the interval between captures.</summary>
    Task WaitForNextCaptureAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        Task.Delay(delay, cancellationToken);

    Task<RealtimeFrameUpdate> ProcessNextAsync(
        Func<RealtimeFrameUpdate, CancellationToken, Task>? partialResultHandler = null,
        CancellationToken cancellationToken = default);
}
