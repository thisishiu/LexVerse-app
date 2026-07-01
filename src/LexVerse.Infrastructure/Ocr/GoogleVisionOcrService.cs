using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexVerse.Core.Models;
using LexVerse.Core.Services;
using LexVerse.Infrastructure.Google;

namespace LexVerse.Infrastructure.Ocr;

public sealed class GoogleVisionOcrService(HttpClient httpClient, GoogleAccessTokenProvider tokenProvider) : IOcrService
{
    public async Task<string> RecognizeTextAsync(ScreenSelection selection, CancellationToken cancellationToken = default)
    {
        var imageContent = ScreenCaptureService.CapturePngBase64(selection);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://vision.googleapis.com/v1/images:annotate")
        {
            Content = JsonContent.Create(new VisionRequest([
                new VisionImageRequest(
                    new VisionImage(imageContent),
                    [new VisionFeature("TEXT_DETECTION", 1)])
            ]))
        };

        request.Headers.Authorization = await tokenProvider.GetAuthenticationHeaderAsync(cancellationToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(GoogleApiErrorFormatter.Format("Google Vision OCR", response.StatusCode, body));
        }

        var result = JsonSerializer.Deserialize<VisionResponse>(body);
        var first = result?.Responses.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(first?.Error?.Message))
        {
            throw new InvalidOperationException($"Google Vision OCR failed: {first.Error.Message}");
        }

        var text = first?.FullTextAnnotation?.Text
            ?? first?.TextAnnotations.FirstOrDefault()?.Description
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("No readable text was found in the selected area.");
        }

        return text.Trim();
    }

    private sealed record VisionRequest([property: JsonPropertyName("requests")] IReadOnlyList<VisionImageRequest> Requests);
    private sealed record VisionImageRequest(
        [property: JsonPropertyName("image")] VisionImage Image,
        [property: JsonPropertyName("features")] IReadOnlyList<VisionFeature> Features);
    private sealed record VisionImage([property: JsonPropertyName("content")] string Content);
    private sealed record VisionFeature(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("maxResults")] int MaxResults);

    private sealed class VisionResponse
    {
        [JsonPropertyName("responses")]
        public List<VisionAnnotateResponse> Responses { get; init; } = [];
    }

    private sealed class VisionAnnotateResponse
    {
        [JsonPropertyName("fullTextAnnotation")]
        public VisionText? FullTextAnnotation { get; init; }

        [JsonPropertyName("textAnnotations")]
        public List<VisionText> TextAnnotations { get; init; } = [];

        [JsonPropertyName("error")]
        public VisionError? Error { get; init; }
    }

    private sealed class VisionText
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }

    private sealed class VisionError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
