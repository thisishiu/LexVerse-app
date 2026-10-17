using System.Reflection;
using LexVerse.Core.Ocr;
using LexVerse.OCR;
using Xunit;

namespace LexVerse.OCR.Tests;

public sealed class VisualTextBlockTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ShortTextClustersWithLargeGapAreSplit(int wordsPerCluster)
    {
        var left = Enumerable.Range(0, wordsPerCluster).Select(i => Word($"left{i}", i * 35)).ToArray();
        var right = Enumerable.Range(0, wordsPerCluster).Select(i => Word($"right{i}", wordsPerCluster * 35 + 40 + i * 35)).ToArray();
        var blocks = Build(left.Concat(right).Reverse().ToArray());

        Assert.Equal(2, blocks.Count);
        Assert.Equal(left, blocks[0].Words);
        Assert.Equal(right, blocks[1].Words);
        Assert.Equal(string.Join(' ', left.Select(word => word.Text)), blocks[0].Text);
        Assert.Equal(string.Join(' ', right.Select(word => word.Text)), blocks[1].Text);
    }

    [Fact]
    public void NormalWordSpacingStaysInOneLine()
    {
        var words = new[] { Word("hello", 0), Word("there", 35), Word("again", 70) };
        var block = Assert.Single(Build(words));
        Assert.Equal(words, block.Words);
        Assert.Equal("hello there again", block.Text);
    }

    private static IReadOnlyList<OcrTextBlock> Build(OcrWord[] words)
    {
        var method = typeof(WindowsOcrService).GetMethod("BuildVisualTextBlocks", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IReadOnlyList<OcrTextBlock>)method.Invoke(null, [words])!;
    }

    private static OcrWord Word(string text, int x) => new(text, new BoundingBox(x, 0, 30, 20), 20);
}
