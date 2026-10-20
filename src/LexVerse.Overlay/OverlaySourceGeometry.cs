using System.Windows;
using System.Windows.Media;
using LexVerse.Core.Geometry;

namespace LexVerse.Overlay;

public static class OverlaySourceGeometry
{
    // Expand in screen pixels, not DIPs, so coverage is consistent across monitors.
    public static ScreenRect GetMaskBounds(ScreenRect source)
    {
        if (source.Width <= 0 || source.Height <= 0) return source;
        var left = Math.Floor(source.X) - 3;
        var top = Math.Floor(source.Y) - 3;
        return new ScreenRect(left, top,
            Math.Ceiling(source.Right) + 3 - left,
            Math.Ceiling(source.Bottom) + 3 - top);
    }

    private static readonly double InkHeightRatio = GetInkHeightRatio();

    // OCR reports ink height, whereas WPF FontSize describes the font's em square.
    public static double FontSizeFromInkHeight(double height) => Math.Max(0, height) / InkHeightRatio;

    private static double GetInkHeightRatio()
    {
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
            FontWeights.SemiBold, FontStretches.Normal);
        return typeface.TryGetGlyphTypeface(out var glyph) && glyph.CapsHeight > 0
            ? glyph.CapsHeight : 0.7;
    }
}
