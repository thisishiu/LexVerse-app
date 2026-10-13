using LexVerse.Core.Ocr;
using LexVerse.OCR;
using Xunit;

namespace LexVerse.OCR.Tests;

public sealed class JapaneseRubyTextFilterTests
{
    [Fact]
    public void RemoveLikelyAnnotations_RemovesSmallJapaneseTextImmediatelyAboveBaseText()
    {
        var ruby = Word("かちょう", 112, 10, 32, 7);
        var baseText = Word("課長", 108, 20, 40, 20);

        var result = JapaneseRubyTextFilter.RemoveLikelyAnnotations([ruby, baseText], "ja-JP");

        Assert.Equal([baseText], result);
    }

    [Fact]
    public void RemoveLikelyAnnotations_RemovesSeveralRubyWordsButKeepsTheBaseLine()
    {
        var baseLeft = Word("残業", 80, 40, 42, 20);
        var baseRight = Word("時間", 150, 40, 42, 20);
        var rubyLeft = Word("ざんぎょう", 82, 28, 38, 7);
        var rubyRight = Word("じかん", 154, 28, 30, 7);

        var result = JapaneseRubyTextFilter.RemoveLikelyAnnotations(
            [rubyLeft, baseLeft, rubyRight, baseRight],
            "ja-JP");

        Assert.Equal([baseLeft, baseRight], result);
    }

    [Fact]
    public void RemoveLikelyAnnotations_RemovesRubyAboveSmallKanaWithADeeperGap()
    {
        var ruby = Word("だ", 239, 36, 8, 9);
        var smallKana = Word("ょ", 241, 59, 14, 19);

        var result = JapaneseRubyTextFilter.RemoveLikelyAnnotations(
            [ruby, smallKana],
            "ja-JP");

        Assert.Equal([smallKana], result);
    }

    [Fact]
    public void RemoveLikelyAnnotations_KeepsSeparateLinesWithSimilarHeight()
    {
        var upperLine = Word("これは前の行です", 40, 10, 180, 18);
        var lowerLine = Word("これは次の行です", 40, 38, 180, 20);

        var result = JapaneseRubyTextFilter.RemoveLikelyAnnotations(
            [upperLine, lowerLine],
            "ja-JP");

        Assert.Equal([upperLine, lowerLine], result);
    }

    [Fact]
    public void RemoveLikelyAnnotations_KeepsSmallTextThatDoesNotOverlapBaseText()
    {
        var smallNote = Word("注釈", 10, 10, 24, 7);
        var baseText = Word("本文", 120, 20, 42, 20);

        var result = JapaneseRubyTextFilter.RemoveLikelyAnnotations(
            [smallNote, baseText],
            "ja-JP");

        Assert.Equal([smallNote, baseText], result);
    }

    [Fact]
    public void RemoveLikelyAnnotations_KeepsSmallTextThatIsTooFarAboveBaseText()
    {
        var smallHeading = Word("見出し", 110, 0, 36, 7);
        var baseText = Word("本文", 108, 40, 42, 20);

        var result = JapaneseRubyTextFilter.RemoveLikelyAnnotations(
            [smallHeading, baseText],
            "ja-JP");

        Assert.Equal([smallHeading, baseText], result);
    }

    [Fact]
    public void RemoveLikelyAnnotations_DoesNotChangeNonJapaneseOcr()
    {
        var smallText = Word("note", 112, 10, 32, 7);
        var baseText = Word("Heading", 108, 20, 60, 20);

        var result = JapaneseRubyTextFilter.RemoveLikelyAnnotations(
            [smallText, baseText],
            "en-US");

        Assert.Equal([smallText, baseText], result);
    }

    private static OcrWord Word(string text, int x, int y, int width, int height) => new(
        text,
        new BoundingBox(x, y, width, height),
        height);
}
