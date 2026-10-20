using System.Net;
using System.Text;
using System.Text.Json;
using LexVerse.Application.Account;
using LexVerse.Application.Diagnostics;
using LexVerse.Application.Popup;
using LexVerse.Application.Privacy;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Translation;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class RemotePopupDocumentTranslatorTests
{
    [Fact]
    public async Task SendsExclusiveTextOrImageWithBearerAndStyleAndReadsTerms()
    {
        var handler = new Handler();
        using var client = new HttpClient(handler);
        var translator = Create(client);
        var options = new PopupTranslationRequest("vi", promptOptions: new TranslationPromptOptions("Natural", ["CPU"], true));
        var result = await translator.TranslateAsync("Hello", null, options, TestContext.Current.CancellationToken);
        Assert.Equal("# Xin chào\n$x^2$", result.TranslationMarkdown);
        Assert.Single(result.Terms);
        using (var wire = JsonDocument.Parse(handler.Body!))
        {
            Assert.Equal("Hello", wire.RootElement.GetProperty("text").GetString());
            Assert.False(wire.RootElement.TryGetProperty("image", out _));
            Assert.Equal("Natural", wire.RootElement.GetProperty("prompt").GetProperty("instruction").GetString());
            Assert.True(wire.RootElement.GetProperty("prompt").GetProperty("preserveAcronymsAndTechnicalTerms").GetBoolean());
        }
        await translator.TranslateAsync(null, [1, 2, 3], options, TestContext.Current.CancellationToken);
        using var image = JsonDocument.Parse(handler.Body!);
        Assert.Equal("AQID", image.RootElement.GetProperty("image").GetString());
        Assert.False(image.RootElement.TryGetProperty("text", out _));
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task RemoteConsentIsRequiredBeforeSendingImages()
    {
        var handler = new Handler();
        using var client = new HttpClient(handler);
        var translator = Create(client, consent: false);
        await Assert.ThrowsAsync<RemoteProcessingConsentRequiredException>(() =>
            translator.TranslateAsync(null, [1], new PopupTranslationRequest("vi"), TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("popup_not_configured", "popup-not-configured")]
    [InlineData("popup_no_text", "popup-no-text")]
    [InlineData("popup_output_incomplete", "popup-output-incomplete")]
    [InlineData("popup_invalid_response", "popup-invalid-response")]
    [InlineData("quota_exceeded", "translation-quota-exceeded")]
    [InlineData("translation_uncertain", "translation-timeout")]
    public async Task MapsKnownErrorsWithoutRetry(string remote, string expected)
    {
        var handler = new Handler { Error = remote };
        using var client = new HttpClient(handler);
        var failure = await Assert.ThrowsAsync<TranslationFailureException>(() => Create(client).TranslateAsync(
            "hello", null, new PopupTranslationRequest("vi"), TestContext.Current.CancellationToken));
        Assert.Equal(expected, failure.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CancellationDoesNotDispatchAndMalformedResultsAreRejected()
    {
        var handler = new Handler { Json = "{\"sourceText\":\"hello\",\"translationMarkdown\":\"text\",\"terms\":null}" };
        using var client = new HttpClient(handler);
        var translator = Create(client);
        var failure = await Assert.ThrowsAsync<TranslationFailureException>(() => translator.TranslateAsync(
            "hello", null, new PopupTranslationRequest("vi"), TestContext.Current.CancellationToken));
        Assert.Equal("popup-invalid-response", failure.Code);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => translator.TranslateAsync(
            "hello", null, new PopupTranslationRequest("vi"), cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }

    private static RemotePopupDocumentTranslator Create(HttpClient client, bool consent = true) => new(client,
        () => new Uri("https://api.example.test/translations/popup"), new Accounts(), new Preferences(consent));

    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }
        public string? Error { get; init; }
        public string Json { get; init; } = "{\"sourceText\":\"Hello\",\"translationMarkdown\":\"# Xin chào\\n$x^2$\",\"terms\":[{\"term\":\"x\",\"translation\":\"x\",\"explanation\":\"Biến số\"}]}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++; Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(Error is null ? HttpStatusCode.OK : HttpStatusCode.BadGateway)
            {
                Content = new StringContent(Error is null ? Json : JsonSerializer.Serialize(new { error = Error }), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class Accounts : IAccountSessionProvider
    {
        public bool IsConfigured => true;
        public Task<AccountSession?> GetSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult<AccountSession?>(null);
        public Task<AccountSession> SignInAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-token");
    }
    private sealed class Preferences(bool consent) : IPrivacyPreferencesService
    {
        public PrivacyPreferences Current => new(AllowRemoteTextProcessing: consent);
        public event EventHandler<PrivacyPreferences>? Changed { add { } remove { } }
        public Task UpdateAsync(PrivacyPreferences preferences, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
