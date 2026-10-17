using LexVerse.Core.Ocr;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OcrGroupingGeometryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnrelatedLinesStaySeparate(bool comic)
    {
        Assert.Equal(2, Group(comic, [Line("hello", 0, 0, 30), Line("again", 50, 24, 30)]).Count);
        Assert.Equal(2, Group(comic, [Line("hello", 0, 0, 100), Line("again", 80, 24, 100)]).Count);
        Assert.Equal(2, Group(comic, [Line("hello", 0, 0, 100), Line("again", 0, 45, 100)]).Count);
        Assert.Equal(2, Group(comic, [Line("hello", 0, 0, 100), Line("again", 0, 24, 100, 35)]).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GradualHorizontalDriftCannotGrowOneCluster(bool comic)
    {
        var blocks = Group(comic, [
            Line("hello", 0, 0, 100),
            Line("there", 15, 24, 100),
            Line("again", 30, 48, 100)
        ]);

        Assert.Equal(2, blocks.Count);
        Assert.Equal("hello there", blocks[0].Text);
        Assert.Equal("again", blocks[1].Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseAlignedLinesMergeWithoutLosingWordsOrReadingOrder(bool comic)
    {
        var first = Line("hello", 10, 0, 100);
        var second = Line("there", 10, 24, 100);
        var third = Line("again", 10, 48, 70);
        var block = Assert.Single(Group(comic, [third, first, second]));

        Assert.Equal("hello there again", block.Text);
        Assert.Equal(new BoundingBox(10, 0, 100, 68), block.Bounds);
        Assert.Equal(first.Words.Concat(second.Words).Concat(third.Words), block.Words);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SideBySideColumnsRemainSeparate(bool comic)
    {
        var blocks = Group(comic, [
            Line("hello", 0, 0, 100), Line("there", 0, 24, 100),
            Line("other", 160, 0, 100), Line("column", 160, 24, 100)
        ]);

        Assert.Equal(new[] { "hello there", "other column" }, blocks.Select(block => block.Text));
    }

    private static IReadOnlyList<OcrTextBlock> Group(bool comic, OcrTextBlock[] lines) => comic
        ? new ComicSpeechBubbleGrouper().GroupLines(lines)
        : new OcrTextBlockLayoutGrouper().GroupLines(lines);

    private static OcrTextBlock Line(string text, int x, int y, int width, int height = 20)
    {
        var bounds = new BoundingBox(x, y, width, height);
        return new OcrTextBlock(text, bounds, [new OcrWord(text, bounds, height)], height);
    }
}
