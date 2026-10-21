using LexVerse.Application.Diagnostics;
using LexVerse.Application.Privacy;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Privacy;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class PrivacyPreferencesTests
{
    [Fact]
    public async Task ConsentTranslator_BlocksEveryRemoteBatchUntilExplicitlyAllowed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var preferences = new MemoryPreferences(new PrivacyPreferences());
        var inner = new RecordingTranslator();
        var translator = new ConsentCheckingTextTranslator(inner, preferences);

        await Assert.ThrowsAsync<RemoteProcessingConsentRequiredException>(() =>
            translator.TranslateBatchAsync(["private text"], "vi", cancellationToken: cancellationToken));
        Assert.Equal(0, inner.CallCount);

        await preferences.UpdateAsync(
            new PrivacyPreferences(AllowRemoteTextProcessing: true), cancellationToken);
        var translated = await translator.TranslateBatchAsync(
            ["allowed text"], "vi", cancellationToken: cancellationToken);

        Assert.Single(translated);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public void ConsentAwareTelemetry_DropsEventsImmediatelyWhenLocalDiagnosticsAreDisabled()
    {
        var preferences = new MemoryPreferences(new PrivacyPreferences(AllowLocalDiagnostics: false));
        var inner = new RecordingTelemetry();
        var telemetry = new ConsentAwareOperationalTelemetry(inner, preferences);

        telemetry.Record(new OperationalEvent
        {
            Kind = OperationalEventKind.ApplicationStartup,
            Outcome = OperationalOutcome.Succeeded
        });

        Assert.Equal(0, inner.Count);
    }

    [Fact]
    public async Task PersistentPreferences_DefaultPrivateAndRoundTripVersionedSettings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-privacy-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "privacy.json");
        using (var first = new PersistentPrivacyPreferencesService(path))
        {
            Assert.False(first.Current.AllowRemoteTextProcessing);
            Assert.True(first.Current.AllowLocalDiagnostics);
            await first.UpdateAsync(
                new PrivacyPreferences(
                    AllowRemoteTextProcessing: true,
                    AllowLocalDiagnostics: false),
                cancellationToken);
        }

        using (var second = new PersistentPrivacyPreferencesService(path))
        {
            Assert.True(second.Current.AllowRemoteTextProcessing);
            Assert.False(second.Current.AllowLocalDiagnostics);
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        Assert.Contains("\"schemaVersion\":1", json, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task FailedSave_KeepsConsentAndCanRetry()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-privacy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var blockedDirectory = Path.Combine(directory, "blocked");
        await File.WriteAllTextAsync(blockedDirectory, "not a directory", TestContext.Current.CancellationToken);
        using var preferences = new PersistentPrivacyPreferencesService(Path.Combine(blockedDirectory, "privacy.json"));
        var changed = 0;
        preferences.Changed += (_, _) => Interlocked.Increment(ref changed);
        try
        {
            await Assert.ThrowsAnyAsync<IOException>(() => preferences.UpdateAsync(new PrivacyPreferences(true, false)));
            Assert.False(preferences.Current.AllowRemoteTextProcessing);
            Assert.Equal(0, changed);
            File.Delete(blockedDirectory);
            await preferences.UpdateAsync(new PrivacyPreferences(true, false));
            Assert.True(preferences.Current.AllowRemoteTextProcessing);
            Assert.Equal(1, changed);
            var translator = new ConsentCheckingTextTranslator(new RecordingTranslator(), preferences);
            await preferences.UpdateAsync(new PrivacyPreferences(false, false));
            await Assert.ThrowsAsync<RemoteProcessingConsentRequiredException>(() => translator.TranslateAsync("private", "vi"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class MemoryPreferences(PrivacyPreferences current) : IPrivacyPreferencesService
    {
        public PrivacyPreferences Current { get; private set; } = current;
        public event EventHandler<PrivacyPreferences>? Changed;
        public Task UpdateAsync(PrivacyPreferences preferences, CancellationToken cancellationToken = default)
        {
            Current = preferences;
            Changed?.Invoke(this, preferences);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTranslator : ITextTranslator
    {
        public int CallCount { get; private set; }
        public Task<TextTranslationResult> TranslateAsync(
            string text,
            string targetLanguage,
            string? sourceLanguage = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new TextTranslationResult(
                text,
                $"translated:{text}",
                targetLanguage,
                sourceLanguage));
        }
    }

    private sealed class RecordingTelemetry : IOperationalTelemetry
    {
        public int Count { get; private set; }
        public void Record(OperationalEvent operationalEvent) => Count++;
    }
}
