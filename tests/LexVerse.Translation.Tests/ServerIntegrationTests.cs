using System.Net;
using System.Text;
using System.Text.Json;
using LexVerse.Application.Account;
using LexVerse.Core.Product;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Product;
using LexVerse.Infrastructure.Translation;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class ServerIntegrationTests
{
    [Fact]
    public void NodeSignedFixture_VerifiesWithDesktopVerifier()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "node-entitlement.json")));
        var verifier = new EntitlementEnvelopeVerifier([
            new EntitlementVerificationKey("node-fixture", fixture.RootElement.GetProperty("publicKey").GetString()!)
        ], new FixtureTimeProvider());
        var bytes = Encoding.UTF8.GetBytes(fixture.RootElement.GetProperty("envelope").GetRawText());
        var snapshot = verifier.Verify(bytes, "node-user");
        Assert.True(snapshot.Evaluate(ProductFeatures.PopupTranslation, new DateTimeOffset(2030, 1, 1, 0, 1, 0, TimeSpan.Zero)).IsAllowed);
        Assert.Throws<EntitlementVerificationException>(() => verifier.Verify(bytes, "other-user"));
    }

    [Fact]
    public async Task RemoteTranslator_SendsBearerBatchAndFeatureContext_PreservesOrderAndTerms()
    {
        var handler = new TranslatorHandler();
        using var client = new HttpClient(handler);
        ITextTranslator translator = new RemoteTextTranslator(client, new Uri("https://api.example.test/translations"), new Accounts(), ["translation.region", "mode.document"]);
        var results = await translator.TranslateBatchAsync(["Keep LexVerse", "Second"], "vi", "en",
            new TranslationPromptOptions(ignoredTerms: ["LexVerse"]), TestContext.Current.CancellationToken);
        Assert.Equal(2, results.Count);
        Assert.Equal("Keep LexVerse", results[0].TranslatedText);
        Assert.Equal("Second", results[1].TranslatedText);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal(new[] { "translation.region", "mode.document" }, handler.Features);
    }

    [Fact]
    public async Task RemoteTranslator_DoesNotRetryChargeableFailures()
    {
        var handler = new TranslatorHandler { Fail = true };
        using var client = new HttpClient(handler);
        var translator = new RemoteTextTranslator(client, new Uri("https://api.example.test/translations"), new Accounts(), ["translation.popup"]);
        var failure = await Assert.ThrowsAsync<LexVerse.Application.Diagnostics.TranslationFailureException>(() => translator.TranslateAsync("Hello", "vi", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("translation-timeout", failure.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("quota_exceeded", "translation-quota-exceeded")]
    [InlineData("unknown_remote_error", "translation-rate-limited")]
    public async Task RemoteTranslator_UsesOnlyKnownErrorCodes(string remoteCode, string expected)
    {
        var handler = new TranslatorHandler { ErrorCode = remoteCode };
        using var client = new HttpClient(handler);
        var translator = new RemoteTextTranslator(client, new Uri("https://api.example.test/translations"), new Accounts(), ["translation.popup"]);
        var failure = await Assert.ThrowsAsync<LexVerse.Application.Diagnostics.TranslationFailureException>(() =>
            translator.TranslateAsync("Hello", "vi", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(expected, failure.Code);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class FixtureTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 1, 0, 1, 0, TimeSpan.Zero);
    }
    private sealed class Accounts : IAccountSessionProvider
    {
        public bool IsConfigured => true;
        public Task<AccountSession?> GetSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult<AccountSession?>(null);
        public Task<AccountSession> SignInAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-token");
    }
    private sealed class TranslatorHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Authorization { get; private set; }
        public string[]? Features { get; private set; }
        public bool Fail { get; init; }
        public string? ErrorCode { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (ErrorCode is not null) return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            { Content = new StringContent(JsonSerializer.Serialize(new { error = ErrorCode, message = "private details" })) };
            Authorization = request.Headers.Authorization?.ToString();
            if (Fail) return new HttpResponseMessage(HttpStatusCode.GatewayTimeout);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.True(Guid.TryParse(body.RootElement.GetProperty("requestId").GetString(), out _));
            Features = body.RootElement.GetProperty("features").EnumerateArray().Select(item => item.GetString()!).ToArray();
            var results = body.RootElement.GetProperty("texts").EnumerateArray().Select(item => new {
                sourceText = item.GetString(), translatedText = item.GetString(), targetLanguage = "vi", sourceLanguage = "en"
            }).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { results }), Encoding.UTF8, "application/json") };
        }
    }
}
