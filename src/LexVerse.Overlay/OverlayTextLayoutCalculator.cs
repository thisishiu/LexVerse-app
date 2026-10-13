using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LexVerse.Overlay;

public readonly record struct OverlayTextLayout(double Width, double Height, double FontSize);

public static class OverlayTextLayoutCalculator
{
    private const double MinimumWidth = 24;
    private const double MinimumHeight = 18;
    private const double BorderAndMeasurementSafety = 3;
    private const double CompactExpansionWidth = 240;
    private const double MaximumSourceWidthFactor = 2.2;
    private const int FontFitIterations = 8;

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

        var safeMaximumWidth = Math.Max(MinimumWidth, maximumWidth);
        var baseWidth = Math.Min(
            safeMaximumWidth,
            Math.Max(MinimumWidth, sourceWidth) + horizontalPadding * 2);
        var height = Math.Max(MinimumHeight, sourceHeight) + verticalPadding * 2;
        var safeMinimumFontSize = Math.Max(1, minimumFontSize);
        var safeRequestedFontSize = Math.Max(safeMinimumFontSize, requestedFontSize);
        var safePixelsPerDip = Math.Max(0.1, pixelsPerDip);

        var singleLineWidth = Measure(text, safeRequestedFontSize, null, safePixelsPerDip)
            .WidthIncludingTrailingWhitespace + horizontalPadding * 2;
        var expansionLimit = Math.Max(
            baseWidth * MaximumSourceWidthFactor,
            Math.Min(CompactExpansionWidth, safeMaximumWidth));
        var width = Math.Min(
            safeMaximumWidth,
            Math.Max(baseWidth, Math.Min(singleLineWidth, expansionLimit)));

        var contentWidth = Math.Max(
            1,
            width - horizontalPadding * 2 - BorderAndMeasurementSafety);
        var contentHeight = Math.Max(
            1,
            height - verticalPadding * 2 - BorderAndMeasurementSafety);
        var fontSize = FindLargestFittingFontSize(
            text,
            contentWidth,
            contentHeight,
            safeRequestedFontSize,
            safeMinimumFontSize,
            safePixelsPerDip);

        return new OverlayTextLayout(width, height, fontSize);
    }

    private static double FindLargestFittingFontSize(
        string text,
        double width,
        double height,
        double requestedFontSize,
        double minimumFontSize,
        double pixelsPerDip)
    {
        if (Fits(text, requestedFontSize, width, height, pixelsPerDip))
        {
            return requestedFontSize;
        }

        if (!Fits(text, minimumFontSize, width, height, pixelsPerDip))
        {
            return minimumFontSize;
        }

        var lower = minimumFontSize;
        var upper = requestedFontSize;
        for (var iteration = 0; iteration < FontFitIterations; iteration++)
        {
            var candidate = (lower + upper) / 2;
            if (Fits(text, candidate, width, height, pixelsPerDip))
            {
                lower = candidate;
            }
            else
            {
                upper = candidate;
            }
        }

        return Math.Floor(lower * 2) / 2;
    }

    private static bool Fits(
        string text,
        double fontSize,
        double width,
        double height,
        double pixelsPerDip)
    {
        var formattedText = Measure(text, fontSize, width, pixelsPerDip);
        formattedText.LineHeight = fontSize;
        return formattedText.Height <= height + 0.5;
    }

    private static FormattedText Measure(
        string text,
        double fontSize,
        double? maximumWidth,
        double pixelsPerDip)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new FontFamily("Segoe UI"),
                FontStyles.Normal,
                FontWeights.SemiBold,
                FontStretches.Normal),
            fontSize,
            Brushes.Black,
            pixelsPerDip);

        if (maximumWidth is { } width)
        {
            formattedText.MaxTextWidth = Math.Max(1, width);
        }

        return formattedText;
    }
}
