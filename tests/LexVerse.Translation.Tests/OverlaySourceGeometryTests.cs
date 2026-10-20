using System.Windows;
using LexVerse.Core.Geometry;
using LexVerse.Overlay;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OverlaySourceGeometryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void MaskRoundsOutwardAndAddsPhysicalPixels(double dpi)
    {
        var source = new ScreenRect(-100.2, 20.8, 50.6, 12.4);
        var mask = OverlaySourceGeometry.GetMaskBounds(source);
        Assert.Equal(new ScreenRect(-104, 17, 58, 20), mask);
        // Simulate a window whose screen origin is (-200, 0).
        var local = new Rect((mask.X + 200) / dpi, mask.Y / dpi,
            mask.Width / dpi, mask.Height / dpi);
        var clipped = OverlayTextLayoutCalculator.ClipToBounds(local, 400 / dpi, 200 / dpi);
        Assert.Equal(local.X, clipped.X, 8);
        Assert.Equal(local.Y, clipped.Y, 8);
        Assert.Equal(local.Width, clipped.Width, 8);
        Assert.Equal(local.Height, clipped.Height, 8);
        Assert.True(clipped.Contains(new Rect((source.X + 200) / dpi, source.Y / dpi,
            source.Width / dpi, source.Height / dpi)));
        var edge = OverlaySourceGeometry.GetMaskBounds(new ScreenRect(0, 0, 100, 40));
        Assert.Equal(new Rect(0, 0, 100 / dpi, 40 / dpi),
            OverlayTextLayoutCalculator.ClipToBounds(
                new Rect(edge.X / dpi, edge.Y / dpi, edge.Width / dpi, edge.Height / dpi),
                100 / dpi, 40 / dpi));
    }

    [Fact]
    public void FontPreferenceCompensatesForInkHeightAndOnlyShrinksWhenNecessary()
    {
        var preferred = OverlaySourceGeometry.FontSizeFromInkHeight(20);
        Assert.InRange(preferred, 24, 32);
        var shortText = OverlayTextLayoutCalculator.Calculate("Chữ gốc", 250, 60, 250,
            preferred, 8, 4, 2, 1);
        Assert.Equal(preferred, shortText.FontSize);
        var longText = OverlayTextLayoutCalculator.Calculate(
            string.Join(' ', Enumerable.Repeat("Bản dịch tiếng Việt có dấu", 20)),
            250, 60, 250, preferred, 8, 4, 2, 1);
        Assert.True(longText.FontSize < preferred);
    }
}
