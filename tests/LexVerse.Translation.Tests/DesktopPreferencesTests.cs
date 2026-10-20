using LexVerse.Application.Desktop;
using LexVerse.Infrastructure.Desktop;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class DesktopPreferencesTests
{
    [Fact]
    public async Task PersistentPreferences_WithRetiredFragmentLimit_PreservesOtherSettings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-desktop-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "desktop.json");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(path, """
                {"schemaVersion":1,"preferences":{"appLanguage":"vi","ocrMode":"comic","scanIntervalMs":750,"maxOcrFragments":24,"showPopupBeforeTranslationFinishes":false}}
                """, cancellationToken);
            using var service = new PersistentDesktopPreferencesService(path);
            Assert.Equal("vi", service.Current.AppLanguage);
            Assert.Equal("comic", service.Current.OcrMode);
            Assert.Equal(750, service.Current.ScanIntervalMs);

            await service.UpdateAsync(service.Current, cancellationToken);
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            Assert.DoesNotContain("maxOcrFragments", json, StringComparison.Ordinal);
            Assert.DoesNotContain("showPopupBeforeTranslationFinishes", json, StringComparison.Ordinal);
            using var reloaded = new PersistentDesktopPreferencesService(path);
            Assert.Equal(service.Current, reloaded.Current);
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
            ScanIntervalMs = 750,
            OverlayOpacityPercent = 72,
            OverlayFontScalePercent = 125,
            UseDarkOverlayBlocks = false,
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
        Assert.Equal(DesktopPreferences.DefaultTranslationInstruction, normalized.TranslationInstruction);
        Assert.Equal("F6", normalized.PopupShortcut);
    }
}
