using System.Windows;
using LexVerse.Overlay;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OverlayTextLayoutCalculatorTests
{
    [Theory]
    [InlineData("A long translation that cannot fit inside the original row", 180, 20, 420, 180)]
    [InlineData("Short", 140, 24, 400, 140)]
    [InlineData("Long translation", 6, 5, 90, 6)]
    [InlineData("First line\nSecond line\nThird line", 36, 18, 90, 36)]
    [InlineData("Near right edge", 180, 20, 12, 12)]
    [InlineData("Outside", 180, 20, 0, 0)]
    public void Calculate_PreservesSourceSizeAndFont(
        string text, double width, double height, double availableWidth, double expectedWidth)
    {
        var layout = OverlayTextLayoutCalculator.Calculate(
            text, width, height, availableWidth, 20, 8, 4, 2, 1.25);

        Assert.Equal(expectedWidth, layout.Width);
        Assert.Equal(height, layout.Height);
        Assert.Equal(20, layout.FontSize);
    }

    [Theory]
    [InlineData(10, 20, 30, 40, 10, 20, 30, 40)]
    [InlineData(-5, -10, 30, 40, 0, 0, 25, 30)]
    [InlineData(95, 90, 30, 40, 95, 90, 5, 10)]
    [InlineData(110, 110, 30, 40, 0, 0, 0, 0)]
    public void ClipToBounds_IntersectsWithoutMovingOrExpandingSource(
        double x, double y, double width, double height,
        double expectedX, double expectedY, double expectedWidth, double expectedHeight)
    {
        var rect = OverlayTextLayoutCalculator.ClipToBounds(new Rect(x, y, width, height), 100, 100);

        Assert.Equal(new Rect(expectedX, expectedY, expectedWidth, expectedHeight), rect);
    }
}
