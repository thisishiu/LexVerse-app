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

        var monitor = _monitorService.GetNearestMonitor(request.TargetScreenRect);
        var monitorBounds = _monitorService.GetMonitorBounds(monitor);
        var monitorDpi = _monitorService.GetMonitorDpi(monitor);
        var captureItem = AtStage("capture", () => GraphicsCaptureItemFactory.CreateForMonitor(monitor));
        var source = new CaptureSourceInfo(
            request.CaptureKind == RealtimeCaptureKind.Region
                ? CaptureSourceKind.Region
                : CaptureSourceKind.Monitor,
            request.CaptureKind == RealtimeCaptureKind.Region
                ? "Selected region"
                : "Full screen",
            MonitorDeviceName: monitor.ToString());
        var captureSession = AtStage("capture", () => WindowsGraphicsCaptureSession.Create(
            captureItem,
            frameSize => new FrameGeometry(
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

            return new WindowsRealtimePipelineSession(captureSession, pipeline);
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
