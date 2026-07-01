using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexVerse.Core.Models;
using LexVerse.Core.Services;
using LexVerse.Infrastructure.Bedrock;
using LexVerse.Infrastructure.Ocr;

namespace LexVerse.Infrastructure.Translation;

public sealed class BedrockPopupTranslationService(HttpClient httpClient, BedrockSettings settings) : ITranslationService
{
    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = $"https://bedrock-runtime.{settings.Region}.amazonaws.com/model/{Uri.EscapeDataString(settings.ModelId)}/converse";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(BuildConverseRequest(request))
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BedrockErrorFormatter.Format(response.StatusCode, body, settings));
        }

        var modelText = ExtractModelText(body);
        return BedrockPopupTranslationParser.Parse(request.SourceText, modelText, request.Selection, request.TextSegments);
    }

    public static object BuildRequestForTesting(TranslationRequest request)
    {
        return BuildConverseRequest(request);
    }

    private static ConverseRequest BuildConverseRequest(TranslationRequest request)
    {
        return new ConverseRequest(
            System: [new ConverseTextBlock(BuildSystemPrompt())],
            Messages:
            [
                new ConverseMessage("user",
                    BuildUserContent(request))
            ],
            InferenceConfig: new ConverseInferenceConfig(MaxTokens: MaxTokensFor(request), Temperature: 0.1));
    }

    private static int MaxTokensFor(TranslationRequest request)
    {
        return request.TextSegments is { Count: > 0 } segments
            ? Math.Min(2600, Math.Max(1200, 500 + segments.Count * 90))
            : 900;
    }

    private static string BuildSystemPrompt()
    {
        return """
        You are Lexverse popup translation engine.
        Return exactly one JSON object and no markdown.
        Translate faithfully for reading comprehension, not just word substitution.
        The selected screenshot region is K. Text fragments inside K are W. Translated fragments are T.
        If the user provides indexed OCR text fragments, use those fragments as W and preserve their indexes exactly.
        Split visible text into reading fragments W even when they are spatially scattered, then translate each W into the corresponding T.
        The popup will display T only. It needs sourceBounds for each W so the app can highlight W directly on K when the user hovers T.
        sourceBounds must tightly cover the exact W fragment, not the translated T, not the whole sentence group, and not the popup.
        Use K's top-left as (0,0) and K's bottom-right as (1,1).
        A keyword is primarily an abbreviation or acronym, such as CPU, API, OCR, LLM, JSON, HTTP, AWS, or UI.
        If a word or phrase can be translated naturally into Vietnamese, do not mark it as a keyword.
        Do not mark common words, simple verbs, simple nouns, translated phrases, or UI labels as keywords.
        Do not create word-by-word mappings.
        Translate as full natural sentences.
        Create source-target mappings for segment alignment, but do not treat those mappings as keywords.
        Write annotation meaning and description in the target language. The description must explain what the keyword means in context, not merely repeat the translation.
        JSON schema:
        {
          "sourceText": "text you read from the image or received from the user",
          "translatedText": "full translated text",
          "tokenLinks": [
            {"index": 0, "sourceToken": "source word or phrase", "targetToken": "target word or phrase", "note": "optional short note"}
          ],
          "segments": [
            {
              "index": 0,
              "sourceText": "one W fragment from K",
              "translatedText": "the matching T fragment",
              "sourceBounds": {"x": 0.0, "y": 0.0, "width": 1.0, "height": 1.0}
            }
          ],
          "annotations": [
            {"term": "keyword", "meaning": "short meaning", "description": "why it matters in context"}
          ]
        }
        sourceBounds values must be relative to K from 0.0 to 1.0, not pixels and not percentages.
        Example: a W fragment starting halfway across K uses "x": 0.5, never "x": 50.
        Return the tightest approximate rectangle covering only that W fragment.
        When indexed OCR fragments are provided, sourceBounds may be omitted because the app already has precise OCR bounds.
        Keep tokenLinks compact, usually 0-6 items. Prefer phrase-level links. Never split every word into separate mappings.
        If the user provides an image, first read the visible text from the image, put it in sourceText, then translate it.
        """;
    }

    private static IReadOnlyList<ConverseContentBlock> BuildUserContent(TranslationRequest request)
    {
        var text = request.TextSegments is { Count: > 0 }
            ? $"""
              Source language: {request.SourceLanguage}
              Target language: {request.TargetLanguage}
              The app already ran Windows OCR and produced precise W fragments with indexes.
              Translate every fragment. Preserve each index exactly. Do not merge fragments. Do not renumber fragments. Return exactly one output segment per input fragment.
              If a fragment is a UI label, heading, word, or short phrase, translate that fragment by itself.
              Return segments with index, sourceText, translatedText. You may omit sourceBounds.
              OCR fragments:
              {BuildIndexedTextSegments(request.TextSegments)}
              """
            : string.IsNullOrWhiteSpace(request.SourceText)
            ? $"""
              Source language: {request.SourceLanguage}
              Target language: {request.TargetLanguage}
              The attached image is a screenshot region selected by the user.
              Read all visible text from the image, split it into spatial text fragments W, translate each W into T, include relative sourceBounds for each W, and annotate only abbreviations or acronyms that remain meaningful in the translated text.
              """
            : $"""
              Source language: {request.SourceLanguage}
              Target language: {request.TargetLanguage}
              Translate this as complete natural sentences. Do not map every word. Annotate only abbreviations or acronyms that remain meaningful in the translated text.
              Text:
              {request.SourceText}
              """;

        var blocks = new List<ConverseContentBlock> { ConverseContentBlock.FromText(text) };

        if (request.Selection is not null && request.TextSegments is not { Count: > 0 })
        {
            var imageBase64 = ScreenCaptureService.CaptureOptimizedJpegBase64(request.Selection);
            blocks.Add(ConverseContentBlock.FromJpegImage(imageBase64));
        }

        return blocks;
    }

    private static string BuildIndexedTextSegments(IReadOnlyList<ScreenTextSegment> segments)
    {
        return JsonSerializer.Serialize(
            segments.Select(segment => new
            {
                index = segment.Index,
                text = segment.Text
            }));
    }

    private static string ExtractModelText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("output")
            .GetProperty("message")
            .GetProperty("content");

        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textElement))
            {
                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        throw new InvalidOperationException("Amazon Bedrock returned no text content.");
    }

    private sealed record ConverseRequest(
        [property: JsonPropertyName("system")] IReadOnlyList<ConverseTextBlock> System,
        [property: JsonPropertyName("messages")] IReadOnlyList<ConverseMessage> Messages,
        [property: JsonPropertyName("inferenceConfig")] ConverseInferenceConfig InferenceConfig);

    private sealed record ConverseMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] IReadOnlyList<ConverseContentBlock> Content);

    private sealed record ConverseTextBlock([property: JsonPropertyName("text")] string Text);

    private sealed class ConverseContentBlock
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; init; }

        [JsonPropertyName("image")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ConverseImageBlock? Image { get; init; }

        public static ConverseContentBlock FromText(string text)
        {
            return new ConverseContentBlock { Text = text };
        }

        public static ConverseContentBlock FromJpegImage(string bytes)
        {
            return new ConverseContentBlock
            {
                Image = new ConverseImageBlock(
                    Format: "jpeg",
                    Source: new ConverseImageSource(Bytes: bytes))
            };
        }
    }

    private sealed record ConverseImageBlock(
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("source")] ConverseImageSource Source);

    private sealed record ConverseImageSource([property: JsonPropertyName("bytes")] string Bytes);

    private sealed record ConverseInferenceConfig(
        [property: JsonPropertyName("maxTokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature);
}
