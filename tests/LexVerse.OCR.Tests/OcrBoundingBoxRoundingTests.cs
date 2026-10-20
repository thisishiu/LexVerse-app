using System.Reflection;
using LexVerse.Core.Ocr;
using LexVerse.OCR;
using Xunit;

namespace LexVerse.OCR.Tests;

public sealed class OcrBoundingBoxRoundingTests
{
    [Theory]
    [InlineData(10.8, 20.8, 5.6, 8.6, 10, 20, 7, 10)]
    [InlineData(10, 20, 5, 8, 10, 20, 5, 8)]
    [InlineData(-0.2, -0.4, 5.6, 8.6, -1, -1, 7, 10)]
    public void IntegerBoxContainsAllFractionalEdges(double x, double y, double width, double height,
        int left, int top, int expectedWidth, int expectedHeight)
    {
        var method = typeof(WindowsOcrService).GetMethod("ToBoundingBox",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var bounds = Assert.IsType<BoundingBox>(method.Invoke(null,
            [new Windows.Foundation.Rect(x, y, width, height)]));
        Assert.Equal(new BoundingBox(left, top, expectedWidth, expectedHeight), bounds);
    }
}
