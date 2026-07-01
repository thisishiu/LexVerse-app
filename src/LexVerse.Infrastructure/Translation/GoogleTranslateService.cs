using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexVerse.Core.Models;
using LexVerse.Core.Services;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Google;

namespace LexVerse.Infrastructure.Translation;

public sealed class GoogleTranslateService(HttpClient httpClient, GoogleAccessTokenProvider tokenProvider) : ITranslationService
{
    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://translation.googleapis.com/language/translate/v2")
        {
            Content = JsonContent.Create(new TranslateRequest(
                request.SourceText,
                request.TargetLanguage,
                request.SourceLanguage == "auto" ? null : request.SourceLanguage,
                "text"))
        };

        httpRequest.Headers.Authorization = await tokenProvider.GetAuthenticationHeaderAsync(cancellationToken);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(GoogleApiErrorFormatter.Format("Google Translate", response.StatusCode, body));
        }

        var result = JsonSerializer.Deserialize<TranslateResponse>(body);
        var translatedText = result?.Data?.Translations.FirstOrDefault()?.TranslatedText;
        if (string.IsNullOrWhiteSpace(translatedText))
        {
            throw new InvalidOperationException("Google Translate returned an empty translation.");
        }

        return new TranslationResult(
            request.SourceText,
            System.Net.WebUtility.HtmlDecode(translatedText),
            TokenLinkBuilder.Build(request.SourceText, System.Net.WebUtility.HtmlDecode(translatedText)),
            BuildAnnotations(request.SourceText));
    }

    private static IReadOnlyList<TermAnnotation> BuildAnnotations(string sourceText)
    {
        return sourceText.Contains("AI", StringComparison.OrdinalIgnoreCase)
            ? [new TermAnnotation("AI", "artificial intelligence", "Technical term detected and preserved for context.")]
            : [];
    }
    private sealed record TranslateRequest(
        [property: JsonPropertyName("q")] string Query,
        [property: JsonPropertyName("target")] string Target,
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("format")] string Format);

    private sealed class TranslateResponse
    {
        [JsonPropertyName("data")]
        public TranslateData? Data { get; init; }

        [JsonPropertyName("error")]
        public TranslateError? Error { get; init; }
    }

    private sealed class TranslateData
    {
        [JsonPropertyName("translations")]
        public List<TranslateItem> Translations { get; init; } = [];
    }

    private sealed class TranslateItem
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; init; }
    }

    private sealed class TranslateError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
