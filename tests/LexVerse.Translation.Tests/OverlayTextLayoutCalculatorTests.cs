using LexVerse.Overlay;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OverlayTextLayoutCalculatorTests
{
    [Fact]
    public void Calculate_LongTranslation_StaysWithinSourceRowAndAvailableWidth()
    {
        var layout = OverlayTextLayoutCalculator.Calculate(
            "Tôi đang rất chật vật để trang trải cuộc sống và cần trao đổi thêm.",
            sourceWidth: 180,
            sourceHeight: 20,
            maximumWidth: 420,
            requestedFontSize: 20,
            minimumFontSize: 8,
            horizontalPadding: 4,
            verticalPadding: 2,
            pixelsPerDip: 1);

        Assert.Equal(24, layout.Height);
        Assert.InRange(layout.Width, 188, 420);
        Assert.InRange(layout.FontSize, 8, 19.99);
    }

    [Fact]
    public void Calculate_ShortTranslation_PreservesRequestedFontSize()
    {
        var layout = OverlayTextLayoutCalculator.Calculate(
            "Xin chào",
            sourceWidth: 140,
            sourceHeight: 24,
            maximumWidth: 400,
            requestedFontSize: 16,
            minimumFontSize: 8,
            horizontalPadding: 4,
            verticalPadding: 2,
            pixelsPerDip: 1);

        Assert.Equal(28, layout.Height);
        Assert.Equal(148, layout.Width);
        Assert.Equal(16, layout.FontSize);
    }

    [Fact]
    public void Calculate_NarrowSource_CanExpandWithoutExceedingOverlayBounds()
    {
        var layout = OverlayTextLayoutCalculator.Calculate(
            "Trưởng phòng",
            sourceWidth: 36,
            sourceHeight: 18,
            maximumWidth: 90,
            requestedFontSize: 16,
            minimumFontSize: 8,
            horizontalPadding: 4,
            verticalPadding: 2,
            pixelsPerDip: 1);

        Assert.InRange(layout.Width, 44, 90);
        Assert.Equal(22, layout.Height);
        Assert.InRange(layout.FontSize, 8, 16);
    }
}
