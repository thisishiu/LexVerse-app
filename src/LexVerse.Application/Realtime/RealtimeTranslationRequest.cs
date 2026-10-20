using LexVerse.Core.Geometry;
using LexVerse.Core.Pipeline;
using LexVerse.Core.Product;
using LexVerse.Core.Capture;

namespace LexVerse.Application.Realtime;

public sealed record RealtimeTranslationRequest
{
    public RealtimeTranslationRequest(
        RealtimeCaptureKind captureKind,
        ScreenRect targetScreenRect,
        string ocrLanguageTag,
        RealtimeTranslationOptions options,
        WindowCaptureTarget? windowTarget = null)
    {
        targetScreenRect.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(ocrLanguageTag);
        ArgumentNullException.ThrowIfNull(options);
        if (options.OcrInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "OCR interval must be positive.");
        }

        if ((captureKind == RealtimeCaptureKind.Window) != (windowTarget is not null) ||
            (windowTarget is not null && (windowTarget.Hwnd == IntPtr.Zero || windowTarget.ProcessId == 0 || windowTarget.ThreadId == 0)))
            throw new ArgumentException("Window capture requires a native target.", nameof(windowTarget));
        WindowTarget = windowTarget;
        CaptureKind = captureKind;
        TargetScreenRect = targetScreenRect;
        OcrLanguageTag = ocrLanguageTag.Trim();
        Options = options;
    }

    public RealtimeCaptureKind CaptureKind { get; }
    public WindowCaptureTarget? WindowTarget { get; }

    public ScreenRect TargetScreenRect { get; }

    public string OcrLanguageTag { get; }

    public RealtimeTranslationOptions Options { get; }

    public FeatureKey RequiredFeature => CaptureKind == RealtimeCaptureKind.Window
        ? ProductFeatures.WindowTranslation
        : CaptureKind == RealtimeCaptureKind.Region
        ? ProductFeatures.RegionTranslation
        : ProductFeatures.FullScreenTranslation;
}
