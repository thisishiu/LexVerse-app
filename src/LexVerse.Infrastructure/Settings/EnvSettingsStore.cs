using LexVerse.Core.Models;
using LexVerse.Core.Services;
using LexVerse.Infrastructure.Configuration;

namespace LexVerse.Infrastructure.Settings;

public sealed class EnvSettingsStore(string envPath) : ISettingsStore
{
    public AppSettings Load()
    {
        var env = EnvFileLoader.Load(envPath);
        var defaults = AppSettings.Default;

        return defaults with
        {
            SourceLanguage = env.GetValueOrDefault("LEXVERSE_SOURCE_LANGUAGE", defaults.SourceLanguage),
            TargetLanguage = env.GetValueOrDefault("LEXVERSE_TARGET_LANGUAGE", defaults.TargetLanguage),
            RealtimeScanIntervalMs = ParseInt(env, "LEXVERSE_REALTIME_SCAN_INTERVAL_MS", defaults.RealtimeScanIntervalMs, 500, 8000),
            RealtimeOverlayOpacity = ParseDouble(env, "LEXVERSE_REALTIME_OVERLAY_OPACITY", defaults.RealtimeOverlayOpacity, 0.45, 1),
            RealtimeFontSize = ParseDouble(env, "LEXVERSE_REALTIME_FONT_SIZE", defaults.RealtimeFontSize, 11, 30),
            RealtimeMaxSegments = ParseInt(env, "LEXVERSE_REALTIME_MAX_SEGMENTS", defaults.RealtimeMaxSegments, 1, 40)
        };
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> env, string key, int fallback, int min, int max)
    {
        return env.TryGetValue(key, out var raw) && int.TryParse(raw, out var value)
            ? Math.Clamp(value, min, max)
            : fallback;
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> env, string key, double fallback, double min, double max)
    {
        return env.TryGetValue(key, out var raw) && double.TryParse(raw, out var value)
            ? Math.Clamp(value, min, max)
            : fallback;
    }
}
