using LexVerse.Application.Desktop;
using LexVerse.Application.Privacy;
using LexVerse.Application.Realtime;
using LexVerse.Core.Product;
using LexVerse.Core.Translation;

namespace LexVerse.App.Demo;

internal sealed class DemoDesktopPreferences(string caseId) : IDesktopPreferencesService
{
    public DesktopPreferences Current { get; private set; } = new()
    {
        AppLanguage = "vi",
        SourceLanguage = caseId switch { "game" => "ja", "letters" => "ko", "comic" => "zh", _ => "en" },
        TargetLanguage = caseId == "game" ? "en" : "vi",
        OcrMode = caseId switch { "game" => "game-dialogue", "comic" => "comic", _ => "document" }
    };
    public event EventHandler<DesktopPreferences>? Changed;
    public Task UpdateAsync(DesktopPreferences preferences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Current = preferences.Normalize();
        Changed?.Invoke(this, Current);
        return Task.CompletedTask;
    }
}

internal sealed class DemoPrivacyPreferences : IPrivacyPreferencesService
{
    public PrivacyPreferences Current { get; private set; } = new(true, false);
    public event EventHandler<PrivacyPreferences>? Changed;
    public Task UpdateAsync(PrivacyPreferences preferences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Current = preferences;
        Changed?.Invoke(this, Current);
        return Task.CompletedTask;
    }
}

internal sealed class DemoEntitlements : IEntitlementProvider
{
    public Task<EntitlementSnapshot> GetEntitlementsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new EntitlementSnapshot("local-recording", "1", DateTimeOffset.UtcNow,
            new[] { ProductFeatures.PopupTranslation, ProductFeatures.RegionTranslation, ProductFeatures.FullScreenTranslation,
                ProductFeatures.WindowTranslation, ProductFeatures.ComicMode, ProductFeatures.DocumentMode, ProductFeatures.CustomTranslationStyle }
                .Select(key => new FeatureEntitlement(key))));
}

// Defensive guards: a missed demo route must never fall through to OCR or network translation.
internal sealed class DemoDisabledPipeline : IRealtimePipelineSessionFactory
{
    public Task<IRealtimePipelineSession> CreateAsync(RealtimeTranslationRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Use the open sample application for this recording session.");
}

internal sealed class DemoDisabledTranslator : ITextTranslator
{
    public Task<TextTranslationResult> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("This recording session only contains the prepared sample texts.");
}
