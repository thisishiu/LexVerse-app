using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LexVerse.Core.Models;
using LexVerse.Core.Translation;

namespace LexVerse.Infrastructure.Bedrock;

public static class BedrockPopupTranslationParser
{
    public static TranslationResult Parse(
        string sourceText,
        string modelText,
        ScreenSelection? selection = null,
        IReadOnlyList<ScreenTextSegment>? sourceSegments = null)
    {
        var payload = TryDeserializePayload(modelText);

        if (payload is null || string.IsNullOrWhiteSpace(payload.TranslatedText))
        {
            return CreateFallbackResult(sourceText, modelText);
        }

        var finalSourceText = string.IsNullOrWhiteSpace(payload.SourceText)
            ? sourceText
            : payload.SourceText.Trim();

        var tokenLinks = payload.TokenLinks
            .Select((link, index) => new TokenLink(
                link.Index ?? index,
                link.SourceToken ?? string.Empty,
                link.TargetToken ?? string.Empty,
                link.Note))
            .Where(link => !string.IsNullOrWhiteSpace(link.SourceToken) || !string.IsNullOrWhiteSpace(link.TargetToken))
            .ToArray();

        var annotations = payload.Annotations
            .Select(item => new TermAnnotation(
                item.Term ?? string.Empty,
                item.Meaning ?? string.Empty,
                item.Description ?? string.Empty))
            .Where(item => !string.IsNullOrWhiteSpace(item.Term))
            .ToArray();

        var segments = payload.Segments
            .Select((segment, index) => new TranslationSegment(
                segment.Index ?? index,
                segment.SourceText ?? string.Empty,
                segment.TranslatedText ?? string.Empty,
                ResolveSourceBounds(segment, selection, sourceSegments)))
            .Where(segment => !string.IsNullOrWhiteSpace(segment.TranslatedText))
            .OrderBy(segment => segment.Index)
            .ToArray();

        if (tokenLinks.Length == 0)
        {
            tokenLinks = TokenLinkBuilder.Build(finalSourceText, payload.TranslatedText).ToArray();
        }

        if (segments.Length == 0)
        {
            segments =
            [
                new TranslationSegment(
                    0,
                    finalSourceText,
                    payload.TranslatedText.Trim(),
                    null)
            ];
        }
        else if (sourceSegments is { Count: > 0 })
        {
            segments = EnsureSegmentsForEveryOcrFragment(segments, sourceSegments);
        }

        return new TranslationResult(finalSourceText, payload.TranslatedText.Trim(), tokenLinks, annotations, segments);
    }

    private static TranslationSegment[] EnsureSegmentsForEveryOcrFragment(
        IReadOnlyList<TranslationSegment> translatedSegments,
        IReadOnlyList<ScreenTextSegment> sourceSegments)
    {
        var translatedByIndex = translatedSegments
            .GroupBy(segment => segment.Index)
            .ToDictionary(group => group.Key, group => group.First());

        return sourceSegments
            .OrderBy(segment => segment.Index)
            .Select(source =>
            {
                if (translatedByIndex.TryGetValue(source.Index, out var translated))
                {
                    return translated with
                    {
                        SourceText = string.IsNullOrWhiteSpace(translated.SourceText) ? source.Text : translated.SourceText,
                        SourceBounds = source.Bounds
                    };
                }

                return new TranslationSegment(source.Index, source.Text, source.Text, source.Bounds);
            })
            .ToArray();
    }

    private static SourceBounds? ResolveSourceBounds(
        BedrockSegment segment,
        ScreenSelection? selection,
        IReadOnlyList<ScreenTextSegment>? sourceSegments)
    {
        if (segment.Index is not null && sourceSegments is not null)
        {
            var source = sourceSegments.FirstOrDefault(item => item.Index == segment.Index);
            if (source is not null)
            {
                return source.Bounds;
            }
        }

        return ToSourceBounds(segment.SourceBounds, selection);
    }

    private static BedrockTranslationPayload? TryDeserializePayload(string modelText)
    {
        var json = ExtractJsonObjectCandidate(modelText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return TryDeserialize(json) ?? TryDeserialize(RepairJsonObject(json));
    }

    private static BedrockTranslationPayload? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BedrockTranslationPayload>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJsonObjectCandidate(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var candidate = text[start..].Trim();
        var fenceIndex = candidate.IndexOf("```", StringComparison.Ordinal);
        return fenceIndex > 0
            ? candidate[..fenceIndex].Trim()
            : candidate;
    }

    private static string RepairJsonObject(string json)
    {
        var stack = new Stack<char>();
        var repaired = new List<char>(json.Length + 8);
        var inString = false;
        var escaping = false;

        foreach (var character in json)
        {
            repaired.Add(character);

            if (escaping)
            {
                escaping = false;
                continue;
            }

            if (character == '\\' && inString)
            {
                escaping = true;
                continue;
            }

            if (character == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (character == '{')
            {
                stack.Push('}');
            }
            else if (character == '[')
            {
                stack.Push(']');
            }
            else if ((character == '}' || character == ']') && stack.Count > 0 && stack.Peek() == character)
            {
                stack.Pop();
            }
        }

        if (inString)
        {
            repaired.Add('"');
        }

        while (stack.Count > 0)
        {
            RemoveTrailingComma(repaired);
            repaired.Add(stack.Pop());
        }

        return new string(repaired.ToArray());
    }

    private static void RemoveTrailingComma(List<char> characters)
    {
        var index = characters.Count - 1;
        while (index >= 0 && char.IsWhiteSpace(characters[index]))
        {
            index--;
        }

        if (index >= 0 && characters[index] == ',')
        {
            characters.RemoveAt(index);
        }
    }

    private static TranslationResult CreateFallbackResult(string sourceText, string modelText)
    {
        var translatedText = TryExtractStringProperty(modelText, "translatedText");
        var finalSourceText = TryExtractStringProperty(modelText, "sourceText") ?? sourceText;

        if (string.IsNullOrWhiteSpace(translatedText))
        {
            translatedText = StripJsonScaffolding(modelText);
        }

        return new TranslationResult(
            finalSourceText.Trim(),
            translatedText.Trim(),
            [],
            [],
            [new TranslationSegment(0, finalSourceText.Trim(), translatedText.Trim(), null)]);
    }

    private static SourceBounds? ToSourceBounds(BedrockBounds? bounds, ScreenSelection? selection)
    {
        if (bounds is null)
        {
            return null;
        }

        var x = FirstValue(bounds.X, bounds.Left, bounds.XMin, bounds.X1);
        var y = FirstValue(bounds.Y, bounds.Top, bounds.YMin, bounds.Y1);
        var width = FirstValue(bounds.Width, bounds.W);
        var height = FirstValue(bounds.Height, bounds.H);
        var right = FirstValue(bounds.Right, bounds.XMax, bounds.X2);
        var bottom = FirstValue(bounds.Bottom, bounds.YMax, bounds.Y2);

        if (width is null && right is not null && x is not null)
        {
            width = right - x;
        }

        if (height is null && bottom is not null && y is not null)
        {
            height = bottom - y;
        }

        if (x is null || y is null || width is null || height is null || width <= 0 || height <= 0)
        {
            return null;
        }

        var normalized = NormalizeBounds(x.Value, y.Value, width.Value, height.Value, selection);
        if (normalized.Width <= 0 || normalized.Height <= 0)
        {
            return null;
        }

        return normalized;
    }

    private static SourceBounds NormalizeBounds(double x, double y, double width, double height, ScreenSelection? selection)
    {
        var max = new[] { x, y, width, height }.Where(value => !double.IsNaN(value) && !double.IsInfinity(value)).DefaultIfEmpty(0).Max();

        if (max > 1 && max <= 100)
        {
            x /= 100;
            y /= 100;
            width /= 100;
            height /= 100;
        }
        else if (max > 100 && selection is not null)
        {
            x /= Math.Max(1, selection.Width);
            width /= Math.Max(1, selection.Width);
            y /= Math.Max(1, selection.Height);
            height /= Math.Max(1, selection.Height);
        }

        x = ClampUnit(x);
        y = ClampUnit(y);
        width = ClampUnit(width);
        height = ClampUnit(height);

        if (x + width > 1)
        {
            width = Math.Max(0, 1 - x);
        }

        if (y + height > 1)
        {
            height = Math.Max(0, 1 - y);
        }

        return new SourceBounds(x, y, width, height);
    }

    private static double? FirstValue(params double?[] values)
    {
        foreach (var value in values)
        {
            if (value is not null && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value))
            {
                return value.Value;
            }
        }

        return null;
    }

    private static double ClampUnit(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(1, value));
    }

    private static string? TryExtractStringProperty(string text, string propertyName)
    {
        var pattern = $"""
            "{Regex.Escape(propertyName)}"\s*:\s*"((?:\\.|[^"\\])*)"
            """;
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string>($"\"{match.Groups[1].Value}\"");
        }
        catch (JsonException)
        {
            return match.Groups[1].Value;
        }
    }

    private static string StripJsonScaffolding(string text)
    {
        return text
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private sealed class BedrockTranslationPayload
    {
        [JsonPropertyName("translatedText")]
        public string TranslatedText { get; init; } = string.Empty;

        [JsonPropertyName("sourceText")]
        public string SourceText { get; init; } = string.Empty;

        [JsonPropertyName("tokenLinks")]
        public List<BedrockTokenLink> TokenLinks { get; init; } = [];

        [JsonPropertyName("annotations")]
        public List<BedrockAnnotation> Annotations { get; init; } = [];

        [JsonPropertyName("segments")]
        public List<BedrockSegment> Segments { get; init; } = [];
    }

    private sealed class BedrockTokenLink
    {
        [JsonPropertyName("index")]
        public int? Index { get; init; }

        [JsonPropertyName("sourceToken")]
        public string? SourceToken { get; init; }

        [JsonPropertyName("targetToken")]
        public string? TargetToken { get; init; }

        [JsonPropertyName("note")]
        public string? Note { get; init; }
    }

    private sealed class BedrockAnnotation
    {
        [JsonPropertyName("term")]
        public string? Term { get; init; }

        [JsonPropertyName("meaning")]
        public string? Meaning { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }

    private sealed class BedrockSegment
    {
        [JsonPropertyName("index")]
        public int? Index { get; init; }

        [JsonPropertyName("sourceText")]
        public string? SourceText { get; init; }

        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; init; }

        [JsonPropertyName("sourceBounds")]
        public BedrockBounds? SourceBounds { get; init; }
    }

    private sealed class BedrockBounds
    {
        [JsonPropertyName("x")]
        public double? X { get; init; }

        [JsonPropertyName("y")]
        public double? Y { get; init; }

        [JsonPropertyName("width")]
        public double? Width { get; init; }

        [JsonPropertyName("height")]
        public double? Height { get; init; }

        [JsonPropertyName("w")]
        public double? W { get; init; }

        [JsonPropertyName("h")]
        public double? H { get; init; }

        [JsonPropertyName("left")]
        public double? Left { get; init; }

        [JsonPropertyName("top")]
        public double? Top { get; init; }

        [JsonPropertyName("right")]
        public double? Right { get; init; }

        [JsonPropertyName("bottom")]
        public double? Bottom { get; init; }

        [JsonPropertyName("xMin")]
        public double? XMin { get; init; }

        [JsonPropertyName("yMin")]
        public double? YMin { get; init; }

        [JsonPropertyName("xMax")]
        public double? XMax { get; init; }

        [JsonPropertyName("yMax")]
        public double? YMax { get; init; }

        [JsonPropertyName("x1")]
        public double? X1 { get; init; }

        [JsonPropertyName("y1")]
        public double? Y1 { get; init; }

        [JsonPropertyName("x2")]
        public double? X2 { get; init; }

        [JsonPropertyName("y2")]
        public double? Y2 { get; init; }
    }
}
