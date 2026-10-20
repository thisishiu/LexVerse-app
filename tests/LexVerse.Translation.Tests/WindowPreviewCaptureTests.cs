using System.IO;
using System.Windows.Media.Imaging;
using LexVerse.App;
using LexVerse.Core.Capture;
using LexVerse.Core.Imaging;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class WindowPreviewCaptureTests
{
    [Theory]
    [InlineData(1920, 1080, 480, 270)]
    [InlineData(1080, 1920, 152, 270)]
    [InlineData(120, 80, 120, 80)]
    public void EncodesSmallInMemoryJpegWithPreservedAspectRatio(int width, int height, int expectedWidth, int expectedHeight)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, (byte)200);
        var result = WindowPreviewCapture.EncodeFrame("opaque", new CapturedFrame(width, height, width * 4, PixelFormat.Bgra8, pixels, DateTimeOffset.UtcNow));
        Assert.Equal("available", result.Status);
        Assert.NotNull(result.ImageDataUrl);
        Assert.StartsWith("data:image/jpeg;base64,", result.ImageDataUrl);
        using var stream = new MemoryStream(Convert.FromBase64String(result.ImageDataUrl!.Split(',')[1]));
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Assert.InRange(frame.PixelWidth, expectedWidth - 1, expectedWidth + 1);
        Assert.Equal(expectedHeight, frame.PixelHeight);
        Assert.Equal("opaque", result.Id);
    }

    [Fact]
    public void BlankCaptureReturnsPlaceholderWithoutImageData()
    {
        var result = WindowPreviewCapture.EncodeFrame("blank", new CapturedFrame(20, 20, 80, PixelFormat.Bgra8, new byte[1600], DateTimeOffset.UtcNow));
        Assert.Equal("unavailable", result.Status);
        Assert.Null(result.ImageDataUrl);
    }

    [Fact]
    public async Task ClosedWindowReturnsClosedWithoutStartingCapture()
    {
        var result = await WindowPreviewCapture.CaptureAsync("gone", new WindowCaptureTarget(IntPtr.Zero, 0, 0, "Closed"), CancellationToken.None);
        Assert.Equal("closed", result.Status);
        Assert.Null(result.ImageDataUrl);
    }
}
