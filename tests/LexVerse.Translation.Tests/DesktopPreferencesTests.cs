using LexVerse.Application.Desktop;
using LexVerse.Infrastructure.Desktop;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class DesktopPreferencesTests
{
    [Fact]
    public async Task PersistentPreferences_RoundTripEveryDesktopSetting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-desktop-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "desktop.json");
        var expected = new DesktopPreferences
        {
            AppLanguage = "vi",
            SourceLanguage = "ja",
            TargetLanguage = "vi",
            OcrMode = "comic",
            IgnoredTerms = "HP, MP, LexVerse",
            PreserveAcronymsAndTechnicalTerms = false,
            ClosePopupOnOutsideClick = false,
            AllowPopupDragging = false,
            ShowOcrDebugBoxes = true,
            ShowPopupBeforeTranslationFinishes = true,
            ScanIntervalMs = 750,
            OverlayOpacityPercent = 72,
            OverlayFontScalePercent = 125,
            UseDarkOverlayBlocks = false,
            MaxOcrFragments = 32,
            TranslationInstruction = "Translate as concise Vietnamese.",
            PopupShortcut = "Ctrl+F6",
            RegionShortcut = "Alt+R",
            FullScreenShortcut = "Alt+F",
            StopShortcut = "Escape",
            ResetShortcut = "Ctrl+Backspace"
        };

        try
        {
            using (var first = new PersistentDesktopPreferencesService(path))
            {
                await first.UpdateAsync(expected, cancellationToken);
            }

            using var second = new PersistentDesktopPreferencesService(path);
            Assert.Equal(expected.Normalize(), second.Current);

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            Assert.Contains("\"schemaVersion\":1", json, StringComparison.Ordinal);
            Assert.Contains("\"scanIntervalMs\":750", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Normalize_RepairsUnsupportedAndOutOfRangeValues()
    {
        var normalized = new DesktopPreferences
        {
            AppLanguage = null!,
            SourceLanguage = "unsupported",
            TargetLanguage = "auto",
            OcrMode = null!,
            ScanIntervalMs = 20,
            OverlayOpacityPercent = 500,
            OverlayFontScalePercent = 10,
            MaxOcrFragments = 500,
            TranslationInstruction = "   ",
            PopupShortcut = "   "
        }.Normalize();

        Assert.Equal("en", normalized.AppLanguage);
        Assert.Equal("auto", normalized.SourceLanguage);
        Assert.Equal("vi", normalized.TargetLanguage);
        Assert.Equal("document", normalized.OcrMode);
        Assert.Equal(250, normalized.ScanIntervalMs);
        Assert.Equal(100, normalized.OverlayOpacityPercent);
        Assert.Equal(60, normalized.OverlayFontScalePercent);
        Assert.Equal(64, normalized.MaxOcrFragments);
        Assert.Equal(DesktopPreferences.DefaultTranslationInstruction, normalized.TranslationInstruction);
        Assert.Equal("F6", normalized.PopupShortcut);
    }
}
