using LexVerse.Core.Ocr;
using LexVerse.OCR;
using Xunit;

namespace LexVerse.OCR.Tests;

public sealed class OcrTextLanguageFilterTests
{
    [Theory]
    [InlineData("これはテストです")]
    [InlineData("ゲームを開始")]
    [InlineData("カタカナ")]
    [InlineData("勇者")]
    [InlineData("「次へ」")]
    public void IsLikelyTextBlock_WithJapaneseLocale_AcceptsTextContainingJapaneseScript(string text)
    {
        Assert.True(OcrTextLanguageFilter.IsLikelyTextBlock(CreateBlock(text), "ja-JP"));
    }

    [Fact]
    public void IsLikelyTextBlock_WithEnglishLocale_RejectsJapaneseText()
    {
        Assert.False(OcrTextLanguageFilter.IsLikelyTextBlock(CreateBlock("勇者の物語"), "en-US"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("123")]
    [InlineData("HP 100")]
    [InlineData("Open the inventory")]
    [InlineData("Xin chào, đây là tiếng Việt.")]
    [InlineData("M'WM み / 0")]
    [InlineData("Ngäy Phän ・ HOC tiéng Nhät ・ field")]
    [InlineData("www.lophoc日本.com")]
    [InlineData("・")]
    public void IsLikelyTextBlock_WithJapaneseLocale_RejectsInsufficientJapaneseEvidence(string text)
    {
        Assert.False(OcrTextLanguageFilter.IsLikelyTextBlock(CreateBlock(text), "ja-JP"));
    }

    [Theory]
    [InlineData("Open the inventory")]
    [InlineData("HP increased")]
    public void IsLikelyTextBlock_WithEnglishLocale_PreservesExistingEnglishText(string text)
    {
        Assert.True(OcrTextLanguageFilter.IsLikelyTextBlock(CreateBlock(text), "en-US"));
    }

    [Theory]
    [InlineData("Windowsの設定", "の設定")]
    [InlineData("勇者HP100", "勇者100")]
    [InlineData("ABCカタカナxyz", "カタカナ")]
    [InlineData("ゲーム・テスト", "ゲーム・テスト")]
    [InlineData("第1章", "第1章")]
    public void FilterRecognizedWord_WithJapaneseLocale_RemovesLatinLetters(
        string text,
        string expected)
    {
        var result = OcrTextLanguageFilter.FilterRecognizedWord(CreateWord(text), "ja-JP");

        Assert.NotNull(result);
        Assert.Equal(expected, result.Text);
        Assert.DoesNotContain(result.Text, character =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
    }

    [Theory]
    [InlineData("HP100")]
    [InlineData("lophoctiengnhat.com")]
    [InlineData("Xin chào")]
    [InlineData("M'WMみ")]
    [InlineData("www.lophoc日本.com")]
    [InlineData("・")]
    public void FilterRecognizedWord_WithJapaneseLocale_DropsNonJapaneseOrWeakMixedWords(string text)
    {
        Assert.Null(OcrTextLanguageFilter.FilterRecognizedWord(CreateWord(text), "ja-JP"));
    }

    [Fact]
    public void FilterRecognizedWord_WithEnglishLocale_PreservesLatinText()
    {
        const string text = "Windows HP 100";

        var result = OcrTextLanguageFilter.FilterRecognizedWord(CreateWord(text), "en-US");

        Assert.NotNull(result);
        Assert.Equal(text, result.Text);
    }

    private static OcrTextBlock CreateBlock(string text) => new(
        text,
        new BoundingBox(0, 0, 160, 32),
        [],
        18);

    private static OcrWord CreateWord(string text) => new(
        text,
        new BoundingBox(0, 0, 160, 32),
        18);
}
