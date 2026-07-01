namespace LexVerse.Core.Models;

public sealed record AppSettings(
    string SourceLanguage,
    string TargetLanguage,
    bool FilterSensitiveTerms,
    bool TranslateOnlyAfterSelection,
    bool AllowFreeOverlayMovement,
    bool StartHidden,
    int RealtimeScanIntervalMs,
    double RealtimeOverlayOpacity,
    double RealtimeFontSize,
    int RealtimeMaxSegments,
    IReadOnlyList<string> IgnoredTerms)
{
    public static AppSettings Default { get; } = new(
        SourceLanguage: "en",
        TargetLanguage: "vi",
        FilterSensitiveTerms: true,
        TranslateOnlyAfterSelection: true,
        AllowFreeOverlayMovement: true,
        StartHidden: false,
        RealtimeScanIntervalMs: 1600,
        RealtimeOverlayOpacity: 0.92,
        RealtimeFontSize: 16,
        RealtimeMaxSegments: 12,
        IgnoredTerms: ["quest", "map", "level", "buff", "craft", "HP", "damage", "cooldown"]);
}
