using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using LexVerse.Core.Geometry;
using LexVerse.Application.Diagnostics;

namespace LexVerse.App;

internal static class PopupRegionCapture
{
    internal static byte[] Capture(ScreenRect region)
    {
        var x = (int)Math.Floor(region.X); var y = (int)Math.Floor(region.Y);
        var width = (int)Math.Ceiling(region.Right) - x; var height = (int)Math.Ceiling(region.Bottom) - y;
        if (width < 12 || height < 12 || width > 8000 || height > 8000 || (long)width * height > 32000000)
            throw new TranslationFailureException("translation-too-large");
        DwmFlush();
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var previous = bitmap == IntPtr.Zero ? IntPtr.Zero : SelectObject(memory, bitmap);
        try
        {
            if (screen == IntPtr.Zero || memory == IntPtr.Zero || bitmap == IntPtr.Zero ||
                !BitBlt(memory, 0, 0, width, height, screen, x, y, 0x40CC0020))
                throw new InvalidOperationException("Screen capture unavailable.");
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            if (stream.Length > 3750000) throw new TranslationFailureException("translation-too-large");
            return stream.ToArray();
        }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(memory, previous);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }
    }

    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int dx, int dy, int width, int height, IntPtr src, int sx, int sy, uint rop);
}
