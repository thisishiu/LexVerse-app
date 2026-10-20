using LexVerse.Application.Realtime;
using LexVerse.Core.Capture;
using LexVerse.Core.Geometry;
using LexVerse.Core.Ocr;
using LexVerse.Core.Pipeline;
using LexVerse.Core.ScreenCapture;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Capture;
using LexVerse.Infrastructure.Windows;
using LexVerse.OCR;

namespace LexVerse.Infrastructure.Realtime;

public sealed class WindowsRealtimePipelineSessionFactory : IRealtimePipelineSessionFactory
{
    private readonly ITextTranslator _translator;
    private readonly ITranslationCache _translationCache;
    private readonly WindowsMonitorService _monitorService;
    private readonly Func<RealtimeTranslationRequest, ITextTranslator>? _translatorFactory;

    public WindowsRealtimePipelineSessionFactory(
        ITextTranslator translator,
        ITranslationCache? translationCache = null,
        WindowsMonitorService? monitorService = null,
        Func<RealtimeTranslationRequest, ITextTranslator>? translatorFactory = null)
    {
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _translationCache = translationCache ?? new InMemoryTranslationCache();
        _monitorService = monitorService ?? new WindowsMonitorService();
        _translatorFactory = translatorFactory;
    }

    public async Task<IRealtimePipelineSession> CreateAsync(
        RealtimeTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var target = request.WindowTarget;
        var tracker = new Win32WindowTracker();
        if (target is not null && !WindowTargetRegistry.IsCurrent(target)) throw new WindowTargetClosedException();
        var monitor = _monitorService.GetNearestMonitor(request.TargetScreenRect);
        var monitorBounds = _monitorService.GetMonitorBounds(monitor);
        var monitorDpi = _monitorService.GetMonitorDpi(monitor);
        var captureItem = AtStage("capture", () => target is null
            ? GraphicsCaptureItemFactory.CreateForMonitor(monitor)
            : GraphicsCaptureItemFactory.CreateForWindow(target.Hwnd));
        var source = target is not null
            ? new CaptureSourceInfo(CaptureSourceKind.Window, target.DisplayName, target.Hwnd)
            : new CaptureSourceInfo(
            request.CaptureKind == RealtimeCaptureKind.Region
                ? CaptureSourceKind.Region
                : CaptureSourceKind.Monitor,
            request.CaptureKind == RealtimeCaptureKind.Region
                ? "Selected region"
                : "Full screen",
            MonitorDeviceName: monitor.ToString());
        var captureSession = AtStage("capture", () => WindowsGraphicsCaptureSession.Create(
            captureItem,
            frameSize => target is not null ? WindowGeometry(target, tracker, frameSize) : new FrameGeometry(
                frameSize,
                monitorBounds,
                CoordinateSpace.FrameLocal,
                monitorDpi.ScaleX,
                monitorDpi.ScaleY,
                0),
            source));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var regionProvider = request.CaptureKind == RealtimeCaptureKind.Region
                ? new ScreenRectOcrRegionProvider(request.TargetScreenRect, request.Options.Mode)
                : null;
            IOcrService ocrService = AtStage("ocr", () => new WindowsOcrService(
                request.OcrLanguageTag,
                request.Options.Mode == OcrProcessingMode.Comic
                    ? OcrTextBlockGroupingMode.ComicSpeechBubbles
                    : OcrTextBlockGroupingMode.Layout));
            var pipeline = new RealtimeTranslationPipeline(
                captureSession,
                new ExactFrameChangeDetector(),
                ocrService,
                _translatorFactory?.Invoke(request) ?? _translator,
                _translationCache,
                request.Options,
                regionProvider);

            IRealtimePipelineSession session = new WindowsRealtimePipelineSession(captureSession, pipeline);
            return target is null ? session : new WindowRealtimePipelineSession(session, target);
        }
        catch
        {
            await captureSession.DisposeAsync();
            throw;
        }
    }

    private static T AtStage<T>(string stage, Func<T> create)
    {
        try { return create(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new PipelineStageException(stage, exception); }
    }

    private static FrameGeometry WindowGeometry(WindowCaptureTarget target, Win32WindowTracker tracker, PixelSize size)
    {
        if (!WindowTargetRegistry.IsCurrent(target)) throw new WindowTargetClosedException();
        var snapshot = tracker.GetSnapshot(target.Hwnd);
        return new(size, snapshot.WindowRect, CoordinateSpace.FrameLocal,
            snapshot.Dpi.ScaleX, snapshot.Dpi.ScaleY, snapshot.Version);
    }

    public sealed class WindowRealtimePipelineSession(IRealtimePipelineSession inner, WindowCaptureTarget target,
        Func<LexVerse.Core.Windows.TrackedWindowSnapshot?>? snapshotProvider = null)
        : IRealtimePipelineSession
    {
        private readonly Win32WindowTracker _tracker = new();
        private readonly object _snapshotGate = new();

        private LexVerse.Core.Windows.TrackedWindowSnapshot? ReadSnapshot()
        {
            lock (_snapshotGate)
                return snapshotProvider is not null ? snapshotProvider()
                    : WindowTargetRegistry.IsCurrent(target) ? _tracker.GetSnapshot(target.Hwnd) : null;
        }

        public async Task WaitForNextCaptureAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.Elapsed < delay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ReadSnapshot() is null) throw new WindowTargetClosedException();
                var remaining = delay - timer.Elapsed;
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(40) ? remaining : TimeSpan.FromMilliseconds(40), cancellationToken);
            }
        }

        public async Task<RealtimeFrameUpdate> ProcessNextAsync(
            Func<RealtimeFrameUpdate, CancellationToken, Task>? partialResultHandler = null,
            CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = ReadSnapshot() ?? throw new WindowTargetClosedException();
                if (!snapshot.IsVisible || snapshot.IsMinimized)
                {
                    await Task.Delay(50, cancellationToken);
                    continue;
                }
                using var iteration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var watch = WatchAsync(snapshot, iteration);
                try
                {
                    var update = await inner.ProcessNextAsync(async (partial, token) =>
                    {
                        if (ReadSnapshot() != snapshot) iteration.Cancel();
                        iteration.Token.ThrowIfCancellationRequested();
                        if (partialResultHandler is not null)
                            await partialResultHandler(Attach(partial), token);
                    }, iteration.Token);
                    iteration.Token.ThrowIfCancellationRequested();
                    if (ReadSnapshot() != snapshot) continue;
                    return Attach(update);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                finally
                {
                    iteration.Cancel();
                    await watch;
                }
            }
        }

        private RealtimeFrameUpdate Attach(RealtimeFrameUpdate update) =>
            update with { OverlayFrame = update.OverlayFrame with { WindowTarget = target } };

        private async Task WatchAsync(LexVerse.Core.Windows.TrackedWindowSnapshot initial, CancellationTokenSource iteration)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(40, iteration.Token);
                    if (ReadSnapshot() != initial)
                    {
                        iteration.Cancel();
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (iteration.IsCancellationRequested) { }
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class WindowsRealtimePipelineSession(
        IScreenCaptureSession captureSession,
        RealtimeTranslationPipeline pipeline) : IRealtimePipelineSession
    {
        public async Task<RealtimeFrameUpdate> ProcessNextAsync(
            Func<RealtimeFrameUpdate, CancellationToken, Task>? partialResultHandler = null,
            CancellationToken cancellationToken = default)
        {
            var result = await pipeline.CaptureRecognizeAndTranslateAsync(
                cancellationToken,
                partialResultHandler is null
                    ? null
                    : (partial, token) => partialResultHandler(ToUpdate(partial), token));

            return ToUpdate(result);
        }

        public ValueTask DisposeAsync() => captureSession.DisposeAsync();

        private static RealtimeFrameUpdate ToUpdate(RealtimeTranslationPipelineResult result) =>
            new(
                result.OverlayFrame,
                result.Timing,
                result.Ocr.OcrResult?.Blocks.Count ?? 0,
                result.TranslatedBlocks.Count,
                result.Ocr.Changed,
                result.UsedCachedTranslation);
    }
}
