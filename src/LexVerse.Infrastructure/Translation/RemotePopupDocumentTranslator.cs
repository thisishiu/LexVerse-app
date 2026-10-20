using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LexVerse.Application.Account;
using LexVerse.Application.Popup;
using LexVerse.Application.Privacy;
using LexVerse.Application.Diagnostics;
using LexVerse.Infrastructure.Commerce;
using LexVerse.Infrastructure.Configuration;

namespace LexVerse.Infrastructure.Translation;

public sealed class RemotePopupDocumentTranslator(HttpClient client, Func<Uri> endpoint,
    IAccountSessionProvider accounts, IPrivacyPreferencesService privacy) : IPopupDocumentTranslator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public async Task<PopupDocument> TranslateAsync(string? text, byte[]? png, PopupTranslationRequest options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!privacy.Current.AllowRemoteTextProcessing) throw new RemoteProcessingConsentRequiredException();
        if ((text is null) == (png is null)) throw new ArgumentException("Supply text or PNG, exclusively.");
        if (png is { Length: > 3750000 }) throw new TranslationFailureException("translation-too-large");
        var target = endpoint();
        BackendEndpointPolicy.Validate(target, nameof(endpoint));
        using var request = new HttpRequestMessage(HttpMethod.Post, target);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await accounts.GetAccessTokenAsync(cancellationToken));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var prompt = options.PromptOptions;
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            requestId = Guid.NewGuid(), text, image = png is null ? null : Convert.ToBase64String(png),
            options.TargetLanguage, options.SourceLanguage,
            prompt = new { instruction = prompt?.Instruction ?? "", ignoredTerms = prompt?.IgnoredTerms ?? [],
                preserveAcronymsAndTechnicalTerms = prompt?.PreserveAcronymsAndTechnicalTerms ?? false }
        }, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var code = TranslationFailure.Code(new HttpRequestException(null, null, response.StatusCode));
            try
            {
                var bytes = await HttpJson.ReadResponseBoundedAsync(response, 16384, cancellationToken);
                using var error = JsonDocument.Parse(bytes);
                if (error.RootElement.ValueKind == JsonValueKind.Object && error.RootElement.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.String)
                    code = value.GetString() switch
                    {
                        "popup_not_configured" => "popup-not-configured",
                        "popup_no_text" => "popup-no-text",
                        "popup_invalid_response" => "popup-invalid-response",
                        "popup_output_incomplete" => "popup-output-incomplete",
                        "translation_too_large" => "translation-too-large",
                        "quota_exceeded" => "translation-quota-exceeded",
                        "translation_uncertain" => "translation-timeout",
                        _ => code
                    };
            }
            catch (JsonException) { }
            throw new TranslationFailureException(code);
        }
        var body = await HttpJson.ReadResponseBoundedAsync(response, 2 * 1024 * 1024, cancellationToken);
        PopupDocument? result;
        try { result = JsonSerializer.Deserialize<PopupDocument>(body, JsonOptions); }
        catch (JsonException exception) { throw new TranslationFailureException("popup-invalid-response", exception); }
        if (result is null || string.IsNullOrWhiteSpace(result.SourceText) || string.IsNullOrWhiteSpace(result.TranslationMarkdown) ||
            result.Terms is null || result.Terms.Any(term => term is null || term.Term is null || term.Translation is null || term.Explanation is null))
            throw new TranslationFailureException("popup-invalid-response");
        return result;
    }
}
