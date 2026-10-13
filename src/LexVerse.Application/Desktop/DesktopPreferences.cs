namespace LexVerse.Application.Desktop;

public sealed record DesktopPreferences
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultTranslationInstruction =
        "Translate naturally and clearly. Keep proper names, technical terms, HP/MP/EXP unchanged. Do not add explanations.";

    private static readonly HashSet<string> SupportedLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "auto", "en", "ja", "ko", "zh", "vi" };
    private static readonly HashSet<string> SupportedModes =
        new(StringComparer.OrdinalIgnoreCase) { "fullscreen", "subtitle", "game-dialogue", "document", "comic" };

    public string AppLanguage { get; init; } = "en";

    public string SourceLanguage { get; init; } = "auto";

    public string TargetLanguage { get; init; } = "vi";

    public string OcrMode { get; init; } = "document";

    public string IgnoredTerms { get; init; } = "HP, MP, EXP";

    public bool PreserveAcronymsAndTechnicalTerms { get; init; } = true;

    public bool ClosePopupOnOutsideClick { get; init; } = true;

    public bool AllowPopupDragging { get; init; } = true;

    public bool ShowOcrDebugBoxes { get; init; }

    public bool ShowPopupBeforeTranslationFinishes { get; init; }

    public int ScanIntervalMs { get; init; } = 500;

    public int OverlayOpacityPercent { get; init; } = 85;

    public int OverlayFontScalePercent { get; init; } = 100;

    public bool UseDarkOverlayBlocks { get; init; } = true;

    public int MaxOcrFragments { get; init; } = 24;

    public string TranslationInstruction { get; init; } = DefaultTranslationInstruction;

    public string PopupShortcut { get; init; } = "F6";

    public string RegionShortcut { get; init; } = "Ctrl+Shift+R";

    public string FullScreenShortcut { get; init; } = "Ctrl+Shift+F";

    public string StopShortcut { get; init; } = "Escape";

    public string ResetShortcut { get; init; } = "Ctrl+Shift+Backspace";

    public DesktopPreferences Normalize() => this with
    {
        AppLanguage = string.Equals(AppLanguage, "vi", StringComparison.OrdinalIgnoreCase) ? "vi" : "en",
        SourceLanguage = NormalizeLanguage(SourceLanguage, "auto", allowAuto: true),
        TargetLanguage = NormalizeLanguage(TargetLanguage, "vi", allowAuto: false),
        OcrMode = OcrMode is not null && SupportedModes.Contains(OcrMode)
            ? OcrMode.ToLowerInvariant()
            : "document",
        IgnoredTerms = NormalizeText(IgnoredTerms, 2_000),
        ScanIntervalMs = Math.Clamp(ScanIntervalMs, 250, 2_000),
        OverlayOpacityPercent = Math.Clamp(OverlayOpacityPercent, 40, 100),
        OverlayFontScalePercent = Math.Clamp(OverlayFontScalePercent, 60, 180),
        MaxOcrFragments = Math.Clamp(MaxOcrFragments, 4, 64),
        TranslationInstruction = NormalizeInstruction(TranslationInstruction),
        PopupShortcut = NormalizeShortcut(PopupShortcut, "F6"),
        RegionShortcut = NormalizeShortcut(RegionShortcut, "Ctrl+Shift+R"),
        FullScreenShortcut = NormalizeShortcut(FullScreenShortcut, "Ctrl+Shift+F"),
        StopShortcut = NormalizeShortcut(StopShortcut, "Escape"),
        ResetShortcut = NormalizeShortcut(ResetShortcut, "Ctrl+Shift+Backspace")
    };

    private static string NormalizeLanguage(string? value, string fallback, bool allowAuto)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return SupportedLanguages.Contains(normalized) && (allowAuto || normalized != "auto")
            ? normalized
            : fallback;
    }

    private static string NormalizeInstruction(string? value)
    {
        var normalized = NormalizeText(value, 4_000);
        return normalized.Length == 0 ? DefaultTranslationInstruction : normalized;
    }

    private static string NormalizeShortcut(string? value, string fallback)
    {
        var normalized = NormalizeText(value, 64);
        return normalized.Length == 0 ? fallback : normalized;
    }

    private static string NormalizeText(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }
}
