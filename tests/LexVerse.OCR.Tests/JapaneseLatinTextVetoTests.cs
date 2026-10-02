using LexVerse.Core.Ocr;
using LexVerse.OCR;
using Xunit;

namespace LexVerse.OCR.Tests;

public sealed class JapaneseLatinTextVetoTests
{
    [Fact]
    public void RemoveLikelyLatinHallucinations_RemovesLatinDominantBilingualRow()
    {
        var japaneseHallucination = CreateLine(
            "\u671D\u5546\u54C1\u5C71\u9762\u8AC7\u65B0\u805E",
            new BoundingBox(80, 120, 620, 34),
            CreateWord("\u671D\u5546\u54C1\u5C71", 80, 120, 250, 34),
            CreateWord("\u9762\u8AC7\u65B0\u805E", 345, 120, 355, 34));
        var vietnameseLine = CreateLine(
            "Dieu 1: Ben A uy thac cho Ben B thuc hien cac dich vu tu van.",
            new BoundingBox(76, 118, 636, 38),
            CreateWord("Dieu", 76, 118, 58, 38),
            CreateWord("Ben", 150, 118, 48, 38),
            CreateWord("A", 207, 118, 18, 38),
            CreateWord("uy", 236, 118, 28, 38),
            CreateWord("thac", 273, 118, 54, 38),
            CreateWord("cho", 337, 118, 44, 38),
            CreateWord("Ben", 391, 118, 48, 38),
            CreateWord("B", 448, 118, 18, 38),
            CreateWord("thuc", 476, 118, 52, 38),
            CreateWord("hien", 538, 118, 52, 38),
            CreateWord("tu van", 600, 118, 112, 38));

        var result = JapaneseLatinTextVeto.RemoveLikelyLatinHallucinations(
            [japaneseHallucination],
            [vietnameseLine],
            0,
            0,
            900,
            600);

        Assert.Empty(result);
    }

    [Fact]
    public void RemoveLikelyLatinHallucinations_KeepsJapaneseWhenLatinReadingIsWeaker()
    {
        var japaneseLine = CreateLine(
            "\u9867\u554F\u5951\u7D04\u66F8",
            new BoundingBox(300, 80, 180, 38),
            CreateWord("\u9867\u554F\u5951\u7D04\u66F8", 300, 80, 180, 38));
        var weakLatinReading = CreateLine(
            "IFK",
            new BoundingBox(315, 80, 120, 38),
            CreateWord("IFK", 315, 80, 120, 38));

        var result = JapaneseLatinTextVeto.RemoveLikelyLatinHallucinations(
            [japaneseLine],
            [weakLatinReading],
            0,
            0,
            900,
            600);

        Assert.Single(result);
        Assert.Equal("\u9867\u554F\u5951\u7D04\u66F8", result[0].Text);
    }

    [Fact]
    public void RemoveLikelyLatinHallucinations_DoesNotUseLatinTextFromAdjacentRow()
    {
        var japaneseLine = CreateLine(
            "\u7B2C\u4E00\u6761\u5951\u7D04\u306E\u6210\u7ACB",
            new BoundingBox(80, 100, 420, 30),
            CreateWord("\u7B2C\u4E00\u6761\u5951\u7D04\u306E\u6210\u7ACB", 80, 100, 420, 30));
        var latinLineBelow = CreateLine(
            "Thanh lap hop dong va cac dieu khoan lien quan",
            new BoundingBox(80, 142, 450, 30),
            CreateWord("Thanh", 80, 142, 70, 30),
            CreateWord("lap", 160, 142, 42, 30),
            CreateWord("hop dong", 212, 142, 100, 30),
            CreateWord("dieu khoan", 322, 142, 130, 30));

        var result = JapaneseLatinTextVeto.RemoveLikelyLatinHallucinations(
            [japaneseLine],
            [latinLineBelow],
            0,
            0,
            900,
            600);

        Assert.Single(result);
    }

    [Fact]
    public void RemoveLikelyLatinHallucinations_RemovesOnlyOverlappingWordOnMixedRow()
    {
        var realJapanese = CreateWord("\u672C\u5F53\u306E\u65E5\u672C\u8A9E", 20, 200, 150, 34);
        var latinHallucination = CreateWord("\u671D\u5546\u54C1", 230, 200, 90, 34);
        var mixedJapaneseLine = CreateLine(
            $"{realJapanese.Text} {latinHallucination.Text}",
            new BoundingBox(20, 200, 300, 34),
            realJapanese,
            latinHallucination);
        var latinLine = CreateLine(
            "Vietnamese text",
            new BoundingBox(220, 198, 120, 38),
            CreateWord("Vietnamese", 220, 198, 78, 38),
            CreateWord("text", 302, 198, 38, 38));

        var result = JapaneseLatinTextVeto.RemoveLikelyLatinHallucinations(
            [mixedJapaneseLine],
            [latinLine],
            0,
            0,
            900,
            600);

        var retained = Assert.Single(result);
        Assert.Same(realJapanese, retained);
    }

    [Fact]
    public void NormalizeBounds_RotatesOcrCoordinatesAroundFrameCenter()
    {
        var result = JapaneseLatinTextVeto.NormalizeBounds(
            new BoundingBox(100, 200, 50, 20),
            90,
            400,
            400);

        Assert.Equal(180, result.Left, 6);
        Assert.Equal(100, result.Top, 6);
        Assert.Equal(200, result.Right, 6);
        Assert.Equal(150, result.Bottom, 6);
    }

    private static OcrTextBlock CreateLine(
        string text,
        BoundingBox bounds,
        params OcrWord[] words)
    {
        return new OcrTextBlock(text, bounds, words, bounds.Height);
    }

    private static OcrWord CreateWord(string text, int x, int y, int width, int height)
    {
        return new OcrWord(text, new BoundingBox(x, y, width, height), height);
    }
}
