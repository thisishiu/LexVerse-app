using LexVerse.Overlay;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OverlayTextLayoutCacheTests
{
    [Fact]
    public void RepeatedIdenticalInputsMeasureOnlyOnce()
    {
        var cache = new OverlayTextLayoutCache();
        for (var tick = 0; tick < 120; tick++) Calculate(cache);
        Assert.Equal(1, cache.MeasurementCount);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void TextSizeDpiAndFontInvalidateMeasurementAndCacheIsBounded()
    {
        var cache = new OverlayTextLayoutCache(3);
        Calculate(cache);
        Calculate(cache, text: "changed");
        Calculate(cache, width: 180);
        Calculate(cache, dpi: 1.5);
        Calculate(cache, font: 24);
        Assert.Equal(5, cache.MeasurementCount);
        Assert.Equal(3, cache.Count);
        Calculate(cache);
        Assert.Equal(6, cache.MeasurementCount);
        cache.Clear();
        Assert.Equal(0, cache.Count);
    }

    private static OverlayTextLayout Calculate(OverlayTextLayoutCache cache, string text = "hello",
        double width = 200, double dpi = 1, double font = 16) =>
        cache.Calculate(text, width, 40, width, font, 8, 4, 2, dpi);
}
