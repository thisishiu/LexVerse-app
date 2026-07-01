using System.Drawing;
using System.Drawing.Imaging;
using LexVerse.Core.Models;

namespace LexVerse.Infrastructure.Ocr;

public static class ScreenCaptureService
{
    private const int DefaultMaxDimension = 960;
    private const long DefaultJpegQuality = 76L;

    public static string CapturePngBase64(ScreenSelection selection)
    {
        return Convert.ToBase64String(CapturePngBytes(selection));
    }

    public static string CaptureOptimizedJpegBase64(ScreenSelection selection)
    {
        return Convert.ToBase64String(CaptureOptimizedJpegBytes(selection));
    }

    public static byte[] CaptureOptimizedJpegBytes(ScreenSelection selection)
    {
        using var bitmap = CaptureBitmap(selection);
        using var optimized = ResizeIfNeeded(bitmap, DefaultMaxDimension);
        using var stream = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, DefaultJpegQuality);
        optimized.Save(stream, encoder, parameters);
        return stream.ToArray();
    }

    public static byte[] CapturePngBytes(ScreenSelection selection)
    {
        using var bitmap = CaptureBitmap(selection);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static Bitmap CaptureBitmap(ScreenSelection selection)
    {
        var bitmap = new Bitmap(selection.Width, selection.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(selection.X, selection.Y, 0, 0, new Size(selection.Width, selection.Height), CopyPixelOperation.SourceCopy);
        }

        return bitmap;
    }

    private static Bitmap ResizeIfNeeded(Bitmap source, int maxDimension)
    {
        var longestSide = Math.Max(source.Width, source.Height);
        if (longestSide <= maxDimension)
        {
            return new Bitmap(source);
        }

        var scale = (double)maxDimension / longestSide;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var resized = new Bitmap(width, height);

        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
        graphics.DrawImage(source, 0, 0, width, height);

        return resized;
    }
}
