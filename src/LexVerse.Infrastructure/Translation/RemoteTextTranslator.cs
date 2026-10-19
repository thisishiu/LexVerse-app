using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexVerse.Application.Account;
using LexVerse.Application.Diagnostics;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Commerce;
using LexVerse.Infrastructure.Configuration;

namespace LexVerse.Infrastructure.Translation;

public sealed class RemoteTextTranslator : ITextTranslator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly HttpClient _client;
    private readonly Uri _endpoint;
    private readonly IAccountSessionProvider _accounts;
    private readonly string[] _features;

    public RemoteTextTranslator(HttpClient client, Uri endpoint, IAccountSessionProvider accounts, IEnumerable<string> features)
    {
        BackendEndpointPolicy.Validate(endpoint, nameof(endpoint));
        _client = client;
        _endpoint = endpoint;
        _accounts = accounts;
        _features = features.Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<TextTranslationResult> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null, CancellationToken cancellationToken = default)
        => (await TranslateBatchAsync([text], targetLanguage, sourceLanguage, cancellationToken))[0];

    public async Task<IReadOnlyList<TextTranslationResult>> TranslateBatchAsync(IReadOnlyList<string> texts, string targetLanguage, string? sourceLanguage = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);
        if (texts.Count == 0) return [];
        var accessToken = await _accounts.GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            requestId = Guid.NewGuid().ToString(), texts, targetLanguage, sourceLanguage, features = _features
        }, JsonOptions), Encoding.UTF8, "application/json");
        // Never automatically repeat a chargeable translation after an ambiguous timeout.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        using var httpResponse = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        if (!httpResponse.IsSuccessStatusCode)
        {
            // Only trust recognized machine codes, never remote error messages.
            var code = TranslationFailure.Code(new HttpRequestException(null, null, httpResponse.StatusCode));
            try
            {
                var errorBytes = await HttpJson.ReadResponseBoundedAsync(httpResponse, 16 * 1024, deadline.Token);
                using var error = JsonDocument.Parse(errorBytes);
                if (error.RootElement.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.String)
                    code = value.GetString() switch
                    {
                        "quota_exceeded" => "translation-quota-exceeded",
                        "translation_uncertain" => "translation-timeout",
                        _ => code
                    };
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException) { }
            throw new TranslationFailureException(code);
        }
        var bytes = await HttpJson.ReadResponseBoundedAsync(httpResponse, 512 * 1024, deadline.Token);
        var response = JsonSerializer.Deserialize<ResponseWire>(bytes, JsonOptions)
            ?? throw new InvalidOperationException("The translation response is empty.");
        if (response.Results is null || response.Results.Count != texts.Count ||
            response.Results.Where((result, index) => result.SourceText != texts[index] || result.TargetLanguage != targetLanguage || result.TranslatedText is null).Any())
            throw new InvalidOperationException("The translation response does not match the request.");
        return response.Results;
    }

    public async Task<IReadOnlyList<TextTranslationResult>> TranslateBatchAsync(IReadOnlyList<string> texts, string targetLanguage,
        string? sourceLanguage, TranslationPromptOptions? promptOptions, CancellationToken cancellationToken = default)
    {
        var protectedTexts = texts.Select(text => TranslationTermProtector.Protect(text, promptOptions)).ToArray();
        var results = await TranslateBatchAsync(protectedTexts.Select(item => item.Text).ToArray(), targetLanguage, sourceLanguage, cancellationToken);
        return results.Select((result, index) => protectedTexts[index].Restore(result)).ToArray();
    }

    private sealed class ResponseWire
    {
        public required List<TextTranslationResult> Results { get; init; }
    }
}
