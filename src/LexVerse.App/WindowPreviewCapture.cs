using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LexVerse.Core.Capture;
using LexVerse.Core.Imaging;
using LexVerse.Infrastructure.Capture;

namespace LexVerse.App;

internal sealed record WindowPreview(string Id, string Status, string? ImageDataUrl = null);

/// <summary>Small, in-memory window previews. Never invokes OCR or translation.</summary>
internal static class WindowPreviewCapture
{
    internal static async Task<WindowPreview> CaptureAsync(string id, WindowCaptureTarget target, CancellationToken token)
    {
        if (!LexVerse.Infrastructure.Windows.WindowTargetRegistry.IsCurrent(target)) return new(id, "closed");
        if (IsIconic(target.Hwnd) || IsProtected(target.Hwnd)) return new(id, "unavailable");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            await using var capture = WindowsGraphicsCaptureSession.Create(GraphicsCaptureItemFactory.CreateForWindow(target.Hwnd));
            var frame = await capture.CaptureFrameAsync(timeout.Token);
            token.ThrowIfCancellationRequested();
            if (!LexVerse.Infrastructure.Windows.WindowTargetRegistry.IsCurrent(target)) return new(id, "closed");
            if (IsProtected(target.Hwnd)) return new(id, "unavailable");
            return EncodeFrame(id, frame);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return new(id, LexVerse.Infrastructure.Windows.WindowTargetRegistry.IsCurrent(target) ? "unavailable" : "closed"); }
    }

    internal static WindowPreview EncodeFrame(string id, CapturedFrame frame)
    {
        // Blank/protected surfaces should show a useful placeholder instead of a black tile.
        var visible = false;
        for (var y = 0; y < frame.Height && !visible; y++)
            for (var x = 0; x < frame.Width; x++)
            {
                var pixel = y * frame.Stride + x * 4;
                if ((frame.Pixels[pixel] | frame.Pixels[pixel + 1] | frame.Pixels[pixel + 2]) != 0) { visible = true; break; }
            }
        if (!visible) return new(id, "unavailable");
        var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Pixels, frame.Stride);
        var scale = Math.Min(1d, Math.Min(480d / frame.Width, 270d / frame.Height));
        var thumbnail = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        var encoder = new JpegBitmapEncoder { QualityLevel = 75 };
        encoder.Frames.Add(BitmapFrame.Create(thumbnail));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return new(id, "available", "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray()));
    }

    private static bool IsProtected(IntPtr hwnd) => GetWindowDisplayAffinity(hwnd, out var affinity) && affinity != 0;
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);
}
