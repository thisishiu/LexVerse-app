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

        var width = Math.Max(0, Math.Min(sourceWidth, maximumWidth));
        var height = Math.Max(0, sourceHeight);
        var requested = Math.Max(0.01, Math.Max(minimumFontSize, requestedFontSize));
        if (width == 0 || height == 0 || text.Length == 0)
            return new OverlayTextLayout(width, height, requested);

        var (padding, border) = GetInsets(width, height, horizontalPadding, verticalPadding);
        var availableWidth = width - padding.Left - padding.Right - border.Left - border.Right;
        var availableHeight = height - padding.Top - padding.Bottom - border.Top - border.Bottom;
        using var measurer = new OverlayTextMeasurer();
        bool Fits(double size) => measurer.Fits(text, size, availableWidth, availableHeight, pixelsPerDip);

        // Test the preferred size first. The minimum parameter only sets the starting
        // preference; fitting may go below it without changing the source rectangle.
        if (Fits(requested)) return new OverlayTextLayout(width, height, requested);
        var lower = 0.01;
        var upper = requested;
        if (!Fits(lower)) return new OverlayTextLayout(width, height, lower);
        // Including the two measurements above, never exceed 24 measurements.
        for (var count = 2; count < 24 && upper - lower > 0.01; count++)
        {
            var candidate = (lower + upper) / 2;
            if (Fits(candidate)) lower = candidate;
            else upper = candidate;
        }
        return new OverlayTextLayout(width, height, lower);
    }

    // Shared with the actual Border so tiny rectangles retain room for their text.
    internal static (Thickness Padding, Thickness Border) GetInsets(
        double width, double height, double horizontalPadding = 4, double verticalPadding = 2)
    {
        static (double Padding, double Border) Axis(double size, double padding) =>
            size > 2 * (padding + 1) ? (padding, 1) : (Math.Min(padding, size / 8), Math.Min(1, size / 8));
        var horizontal = Axis(Math.Max(0, width), Math.Max(0, horizontalPadding));
        var vertical = Axis(Math.Max(0, height), Math.Max(0, verticalPadding));
        return (new Thickness(horizontal.Padding, vertical.Padding, horizontal.Padding, vertical.Padding),
            new Thickness(horizontal.Border, vertical.Border, horizontal.Border, vertical.Border));
    }

    public static Rect ClipToBounds(Rect source, double width, double height)
    {
        var clipped = Rect.Intersect(source, new Rect(0, 0, Math.Max(0, width), Math.Max(0, height)));
        return clipped.IsEmpty ? new Rect(0, 0, 0, 0) : clipped;
    }
}
