using LexVerse.Application.Realtime;
using LexVerse.Core.Capture;
using LexVerse.Core.Geometry;
using LexVerse.Core.Overlay;
using LexVerse.Core.Pipeline;
using LexVerse.Core.Windows;
using LexVerse.Infrastructure.Realtime;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class WindowPipelineSessionTests
{
    private static readonly WindowCaptureTarget Target = new(new IntPtr(123), 10, 20, "Editor");
    private static TrackedWindowSnapshot Snapshot(bool minimized = false, long version = 1) =>
        new(Target.Hwnd, new ScreenRect(-900, 40, 800, 600), new ScreenRect(-900, 40, 800, 600),
            true, minimized, new DpiInfo(144, 144), version);

    [Fact]
    public async Task MinimizedWindowWaitsForRestoreAndThenAttachesTarget()
    {
        var current = Snapshot(minimized: true);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Session((_, _) => Task.FromResult(Update()));
        await using var session = new WindowsRealtimePipelineSessionFactory.WindowRealtimePipelineSession(inner, Target, () =>
        { waiting.TrySetResult(); return current; });
        var task = session.ProcessNextAsync(cancellationToken: TestContext.Current.CancellationToken);
        await waiting.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, inner.Calls);
        current = Snapshot(version: 2);
        var result = await task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Target, result.OverlayFrame.WindowTarget);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task GeometryChangeCancelsPendingWorkAndDiscardsStalePartial()
    {
        var current = Snapshot();
        var partials = 0;
        var inner = new Session(async (call, partial) =>
        {
            if (call == 1)
            {
                current = Snapshot(version: 2);
                await partial!(Update(), CancellationToken.None);
            }
            return Update();
        });
        await using var session = new WindowsRealtimePipelineSessionFactory.WindowRealtimePipelineSession(inner, Target, () => current);
        await session.ProcessNextAsync((_, _) => { partials++; return Task.CompletedTask; }, TestContext.Current.CancellationToken);
        Assert.Equal(2, inner.Calls);
        Assert.Equal(0, partials);
    }

    [Fact]
    public async Task ClosingWindowCancelsCaptureWaitAndDisposalReleasesSession()
    {
        TrackedWindowSnapshot? current = Snapshot();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Session(async (_, _) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, Session.CurrentToken);
            return Update();
        });
        var session = new WindowsRealtimePipelineSessionFactory.WindowRealtimePipelineSession(inner, Target, () => current);
        var task = session.ProcessNextAsync(cancellationToken: TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        current = null;
        await Assert.ThrowsAsync<WindowTargetClosedException>(() => task);
        await session.DisposeAsync();
        Assert.True(inner.Disposed);
    }

    [Fact]
    public async Task ClosingWindowInterruptsLongOcrInterval()
    {
        TrackedWindowSnapshot? current = Snapshot();
        var inner = new Session((_, _) => Task.FromResult(Update()));
        await using var session = new WindowsRealtimePipelineSessionFactory.WindowRealtimePipelineSession(inner, Target, () => current);
        var wait = session.WaitForNextCaptureAsync(TimeSpan.FromHours(1), TestContext.Current.CancellationToken);
        current = null;
        await Assert.ThrowsAsync<WindowTargetClosedException>(() => wait.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static RealtimeFrameUpdate Update() => new(new OverlayRenderFrame([], null, 0, DateTimeOffset.UtcNow),
        new RealtimeTranslationPipelineTiming(default, default, default, default, default, default, 0, 0), 0, 0, true, false);

    private sealed class Session(Func<int, Func<RealtimeFrameUpdate, CancellationToken, Task>?, Task<RealtimeFrameUpdate>> process)
        : IRealtimePipelineSession
    {
        // AsyncLocal keeps cancellation isolated when the test runner executes tests concurrently.
        private static readonly AsyncLocal<CancellationToken> Token = new();
        public static CancellationToken CurrentToken => Token.Value;
        public int Calls { get; private set; }
        public bool Disposed { get; private set; }
        public Task<RealtimeFrameUpdate> ProcessNextAsync(Func<RealtimeFrameUpdate, CancellationToken, Task>? partialResultHandler = null,
            CancellationToken cancellationToken = default)
        {
            Token.Value = cancellationToken;
            return process(++Calls, partialResultHandler);
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
