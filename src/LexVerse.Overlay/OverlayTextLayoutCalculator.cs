using System.Windows;

namespace LexVerse.Overlay;

public readonly record struct OverlayTextLayout(double Width, double Height, double FontSize);

public static class OverlayTextLayoutCalculator
{
    public static OverlayTextLayout Calculate(
        string text,
        double sourceWidth,
        double sourceHeight,
        double maximumWidth,
        double requestedFontSize,
        double minimumFontSize,
        double horizontalPadding,
        double verticalPadding,
        double pixelsPerDip)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Padding and borders belong inside the OCR rectangle, never around it.
        return new OverlayTextLayout(
            Math.Max(0, Math.Min(sourceWidth, maximumWidth)),
            Math.Max(0, sourceHeight),
            Math.Max(Math.Max(1, minimumFontSize), requestedFontSize));
    }

    public static Rect ClipToBounds(Rect source, double width, double height)
    {
        var clipped = Rect.Intersect(source, new Rect(0, 0, Math.Max(0, width), Math.Max(0, height)));
        return clipped.IsEmpty ? new Rect(0, 0, 0, 0) : clipped;
    }
}
