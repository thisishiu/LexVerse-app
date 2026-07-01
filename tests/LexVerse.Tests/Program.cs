using System.Net;
using System.Text.Json;
using LexVerse.Core.Models;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Bedrock;
using LexVerse.Infrastructure.Google;
using LexVerse.Infrastructure.Settings;
using LexVerse.Infrastructure.Translation;

var tests = new (string Name, Action Test)[]
{
    ("Google Vision service-disabled errors are concise and actionable", GoogleVisionServiceDisabledErrorIsActionable),
    ("Bedrock settings convert base Anthropic model IDs to inference profile IDs", BedrockSettingsConvertsAnthropicBaseModelToInferenceProfile),
    ("Bedrock settings do not guess APAC profile IDs", BedrockSettingsDoesNotGuessApacInferenceProfile),
    ("Bedrock on-demand errors explain inference profiles", BedrockOnDemandErrorExplainsInferenceProfiles),
    ("Bedrock invalid model errors explain region and profile", BedrockInvalidModelErrorExplainsRegionAndProfile),
    ("Bedrock request does not send both temperature and topP", BedrockRequestDoesNotSendTemperatureAndTopP),
    ("Bedrock prompt defines keywords narrowly", BedrockPromptDefinesKeywordsNarrowly),
    ("Bedrock popup payload maps translation, tokens, and annotations", BedrockPopupPayloadMapsStructuredData),
    ("Bedrock popup payload maps translated segments with source bounds", BedrockPopupPayloadMapsSegmentsWithSourceBounds),
    ("Bedrock parser normalizes percent and pixel source bounds", BedrockParserNormalizesPercentAndPixelSourceBounds),
    ("Bedrock parser prefers OCR source bounds by index", BedrockParserPrefersOcrSourceBoundsByIndex),
    ("Bedrock parser keeps one output segment per OCR fragment", BedrockParserKeepsOneOutputSegmentPerOcrFragment),
    ("Bedrock parser repairs truncated JSON payloads", BedrockParserRepairsTruncatedJsonPayloads),
    ("Settings load realtime options with safe bounds", SettingsLoadRealtimeOptionsWithSafeBounds),
    ("Token linker preserves source and target token order", TokenLinkerPreservesOrder),
    ("Token linker keeps unmatched translated tokens", TokenLinkerKeepsUnmatchedTargetTokens)
};

var failures = new List<string>();

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.Message}");
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(exception);
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Failures:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Environment.Exit(1);
}

static void GoogleVisionServiceDisabledErrorIsActionable()
{
    const string body = """
    {
      "error": {
        "code": 403,
        "message": "Cloud Vision API has not been used in project 331236173965 before or it is disabled.",
        "status": "PERMISSION_DENIED",
        "details": [
          {
            "@type": "type.googleapis.com/google.rpc.ErrorInfo",
            "reason": "SERVICE_DISABLED",
            "domain": "googleapis.com",
            "metadata": {
              "service": "vision.googleapis.com"
            }
          },
          {
            "@type": "type.googleapis.com/google.rpc.Help",
            "links": [
              {
                "description": "Google developers console API activation",
                "url": "https://console.developers.google.com/apis/api/vision.googleapis.com/overview?project=331236173965"
              }
            ]
          }
        ]
      }
    }
    """;

    var message = GoogleApiErrorFormatter.Format("Google Vision OCR", HttpStatusCode.Forbidden, body);

    AssertContains(message, "Google Vision OCR is not ready.");
    AssertContains(message, "Enable Cloud Vision API");
    AssertContains(message, "https://console.developers.google.com/apis/api/vision.googleapis.com/overview?project=331236173965");
    AssertDoesNotContain(message, "\"details\"");
    AssertDoesNotContain(message, "\"@type\"");
}

static void BedrockSettingsConvertsAnthropicBaseModelToInferenceProfile()
{
    var normalized = BedrockSettings.NormalizeModelId("anthropic.claude-haiku-4-5-20251001-v1:0", "us-east-1");

    AssertEqual("us.anthropic.claude-haiku-4-5-20251001-v1:0", normalized);
    AssertEqual("us.anthropic.claude-haiku-4-5-20251001-v1:0", BedrockSettings.NormalizeModelId(normalized, "us-east-1"));
}

static void BedrockSettingsDoesNotGuessApacInferenceProfile()
{
    var normalized = BedrockSettings.NormalizeModelId("anthropic.claude-haiku-4-5-20251001-v1:0", "ap-southeast-1");

    AssertEqual("anthropic.claude-haiku-4-5-20251001-v1:0", normalized);
}

static void BedrockOnDemandErrorExplainsInferenceProfiles()
{
    const string body = """
    {"message":"Invocation of model ID anthropic.claude-haiku-4-5-20251001-v1:0 with on-demand throughput isn't supported. Retry your request with the ID or ARN of an inference profile that contains this model."}
    """;

    var message = BedrockErrorFormatter.Format(
        HttpStatusCode.BadRequest,
        body,
        new BedrockSettings("hidden", "us-east-1", "us.anthropic.claude-haiku-4-5-20251001-v1:0"));

    AssertContains(message, "Use an inference profile ID or ARN");
    AssertContains(message, "us.anthropic.claude-haiku-4-5-20251001-v1:0");
    AssertDoesNotContain(message, "{\"message\"");
}

static void BedrockInvalidModelErrorExplainsRegionAndProfile()
{
    const string body = """
    {"message":"The provided model identifier is invalid."}
    """;

    var message = BedrockErrorFormatter.Format(
        HttpStatusCode.BadRequest,
        body,
        new BedrockSettings("hidden", "ap-southeast-1", "apac.anthropic.claude-haiku-4-5-20251001-v1:0"));

    AssertContains(message, "Current region: ap-southeast-1");
    AssertContains(message, "valid Bedrock inference profile ID/ARN");
    AssertDoesNotContain(message, "{\"message\"");
}

static void BedrockRequestDoesNotSendTemperatureAndTopP()
{
    var request = new TranslationRequest("Hello", "en", "vi");
    var payload = BedrockPopupTranslationService.BuildRequestForTesting(request);
    var json = JsonSerializer.Serialize(payload);

    AssertContains(json, "\"temperature\"");
    AssertDoesNotContain(json, "\"topP\"");
}

static void BedrockPromptDefinesKeywordsNarrowly()
{
    var request = new TranslationRequest("The CPU schedules threads.", "en", "vi");
    var payload = BedrockPopupTranslationService.BuildRequestForTesting(request);
    var json = JsonSerializer.Serialize(payload);

    AssertContains(json, "abbreviation");
    AssertContains(json, "acronym");
    AssertContains(json, "can be translated naturally into Vietnamese");
    AssertContains(json, "sourceBounds");
    AssertContains(json, "relative to K");
    AssertContains(json, "Do not mark common words");
    AssertContains(json, "description must explain what the keyword means in context");
}

static void TokenLinkerPreservesOrder()
{
    var links = TokenLinkBuilder.Build("AI helps me", "AI giup toi");

    AssertEqual(3, links.Count);
    AssertEqual("AI", links[0].SourceToken);
    AssertEqual("AI", links[0].TargetToken);
    AssertEqual("helps", links[1].SourceToken);
    AssertEqual("giup", links[1].TargetToken);
    AssertEqual("me", links[2].SourceToken);
    AssertEqual("toi", links[2].TargetToken);
}

static void SettingsLoadRealtimeOptionsWithSafeBounds()
{
    var path = Path.Combine(Path.GetTempPath(), $"lexverse-settings-{Guid.NewGuid():N}.env");
    File.WriteAllText(path, """
    LEXVERSE_SOURCE_LANGUAGE=ja
    LEXVERSE_TARGET_LANGUAGE=vi
    LEXVERSE_REALTIME_SCAN_INTERVAL_MS=120
    LEXVERSE_REALTIME_OVERLAY_OPACITY=4
    LEXVERSE_REALTIME_FONT_SIZE=8
    LEXVERSE_REALTIME_MAX_SEGMENTS=99
    """);

    try
    {
        var settings = new EnvSettingsStore(path).Load();

        AssertEqual("ja", settings.SourceLanguage);
        AssertEqual("vi", settings.TargetLanguage);
        AssertEqual(500, settings.RealtimeScanIntervalMs);
        AssertEqual(1d, settings.RealtimeOverlayOpacity);
        AssertEqual(11d, settings.RealtimeFontSize);
        AssertEqual(40, settings.RealtimeMaxSegments);
    }
    finally
    {
        File.Delete(path);
    }
}

static void BedrockPopupPayloadMapsStructuredData()
{
    const string modelText = """
    {
      "sourceText": "AI helps me learn faster.",
      "translatedText": "AI giup toi hoc nhanh hon.",
      "tokenLinks": [
        {"index": 0, "sourceToken": "AI", "targetToken": "AI", "note": "technical term"},
        {"index": 1, "sourceToken": "helps me", "targetToken": "giup toi"}
      ],
      "annotations": [
        {"term": "AI", "meaning": "artificial intelligence", "description": "important technical keyword"}
      ]
    }
    """;

    var result = BedrockPopupTranslationParser.Parse("AI helps me learn faster.", modelText);

    AssertEqual("AI helps me learn faster.", result.SourceText);
    AssertEqual("AI giup toi hoc nhanh hon.", result.TranslatedText);
    AssertEqual(2, result.TokenLinks.Count);
    AssertEqual("helps me", result.TokenLinks[1].SourceToken);
    AssertEqual("giup toi", result.TokenLinks[1].TargetToken);
    AssertEqual(1, result.Annotations.Count);
    AssertEqual("AI", result.Annotations[0].Term);
}

static void BedrockPopupPayloadMapsSegmentsWithSourceBounds()
{
    const string modelText = """
    {
      "sourceText": "CPU usage\nGPU memory",
      "translatedText": "Muc su dung CPU\nBo nho GPU",
      "segments": [
        {
          "index": 1,
          "sourceText": "GPU memory",
          "translatedText": "Bo nho GPU",
          "sourceBounds": {"x": 0.52, "y": 0.64, "width": 0.33, "height": 0.12}
        },
        {
          "index": 0,
          "sourceText": "CPU usage",
          "translatedText": "Muc su dung CPU",
          "sourceBounds": {"x": 0.08, "y": 0.18, "width": 0.28, "height": 0.10}
        }
      ],
      "tokenLinks": [],
      "annotations": []
    }
    """;

    var result = BedrockPopupTranslationParser.Parse(string.Empty, modelText);

    AssertEqual(2, result.Segments?.Count ?? 0);
    AssertEqual("CPU usage", result.Segments![0].SourceText);
    AssertEqual("Muc su dung CPU", result.Segments[0].TranslatedText);
    AssertEqual(0.08, result.Segments[0].SourceBounds!.X);
    AssertEqual(0.18, result.Segments[0].SourceBounds!.Y);
    AssertEqual("GPU memory", result.Segments[1].SourceText);
}

static void BedrockParserNormalizesPercentAndPixelSourceBounds()
{
    const string percentModelText = """
    {
      "sourceText": "CPU",
      "translatedText": "CPU",
      "segments": [
        {
          "index": 0,
          "sourceText": "CPU",
          "translatedText": "CPU",
          "sourceBounds": {"left": 25, "top": 40, "right": 50, "bottom": 60}
        }
      ],
      "tokenLinks": [],
      "annotations": []
    }
    """;

    var percent = BedrockPopupTranslationParser.Parse(string.Empty, percentModelText);

    AssertEqual(0.25, percent.Segments![0].SourceBounds!.X);
    AssertEqual(0.40, percent.Segments[0].SourceBounds!.Y);
    AssertEqual(0.25, percent.Segments[0].SourceBounds!.Width);
    AssertEqual(0.20, percent.Segments[0].SourceBounds!.Height);

    const string pixelModelText = """
    {
      "sourceText": "API",
      "translatedText": "API",
      "segments": [
        {
          "index": 0,
          "sourceText": "API",
          "translatedText": "API",
          "sourceBounds": {"x": 240, "y": 90, "width": 120, "height": 45}
        }
      ],
      "tokenLinks": [],
      "annotations": []
    }
    """;

    var pixel = BedrockPopupTranslationParser.Parse(string.Empty, pixelModelText, new ScreenSelection(10, 20, 480, 180));

    AssertEqual(0.5, pixel.Segments![0].SourceBounds!.X);
    AssertEqual(0.5, pixel.Segments[0].SourceBounds!.Y);
    AssertEqual(0.25, pixel.Segments[0].SourceBounds!.Width);
    AssertEqual(0.25, pixel.Segments[0].SourceBounds!.Height);
}

static void BedrockParserPrefersOcrSourceBoundsByIndex()
{
    const string modelText = """
    {
      "sourceText": "CPU\nAPI",
      "translatedText": "CPU\nAPI",
      "segments": [
        {
          "index": 1,
          "sourceText": "API",
          "translatedText": "API",
          "sourceBounds": {"x": 0.01, "y": 0.01, "width": 0.01, "height": 0.01}
        }
      ],
      "tokenLinks": [],
      "annotations": []
    }
    """;

    var ocrSegments = new[]
    {
        new ScreenTextSegment(1, "API", new SourceBounds(0.4, 0.5, 0.2, 0.1))
    };

    var result = BedrockPopupTranslationParser.Parse(string.Empty, modelText, sourceSegments: ocrSegments);

    AssertEqual(0.4, result.Segments![0].SourceBounds!.X);
    AssertEqual(0.5, result.Segments[0].SourceBounds!.Y);
    AssertEqual(0.2, result.Segments[0].SourceBounds!.Width);
    AssertEqual(0.1, result.Segments[0].SourceBounds!.Height);
}

static void BedrockParserKeepsOneOutputSegmentPerOcrFragment()
{
    const string modelText = """
    {
      "sourceText": "Start Capture Realtime Translate",
      "translatedText": "Bắt đầu chụp Dịch thời gian thực",
      "segments": [
        {
          "index": 0,
          "sourceText": "Start Capture",
          "translatedText": "Bắt đầu chụp"
        }
      ],
      "tokenLinks": [],
      "annotations": []
    }
    """;

    var ocrSegments = new[]
    {
        new ScreenTextSegment(0, "Start Capture", new SourceBounds(0.1, 0.1, 0.2, 0.1)),
        new ScreenTextSegment(1, "Realtime Translate", new SourceBounds(0.5, 0.1, 0.3, 0.1))
    };

    var result = BedrockPopupTranslationParser.Parse(string.Empty, modelText, sourceSegments: ocrSegments);

    AssertEqual(2, result.Segments!.Count);
    AssertEqual(0, result.Segments[0].Index);
    AssertEqual("Bắt đầu chụp", result.Segments[0].TranslatedText);
    AssertEqual(1, result.Segments[1].Index);
    AssertEqual("Realtime Translate", result.Segments[1].TranslatedText);
}

static void BedrockParserRepairsTruncatedJsonPayloads()
{
    const string modelText = """
    {
      "sourceText": "The CPU schedules threads.",
      "translatedText": "CPU lập lịch các luồng.",
      "tokenLinks": [
        {"index": 0, "sourceToken": "CPU", "targetToken": "CPU", "note": "acronym"}
      ],
      "annotations": [
        {"term": "CPU", "meaning": "bộ xử lý trung tâm", "description": "Thuật ngữ chỉ bộ xử lý chính của máy tính."}
    """;

    var result = BedrockPopupTranslationParser.Parse("fallback", modelText);

    AssertEqual("The CPU schedules threads.", result.SourceText);
    AssertEqual("CPU lập lịch các luồng.", result.TranslatedText);
    AssertEqual(1, result.TokenLinks.Count);
    AssertEqual("CPU", result.TokenLinks[0].TargetToken);
    AssertEqual(1, result.Annotations.Count);
    AssertEqual("CPU", result.Annotations[0].Term);
}

static void TokenLinkerKeepsUnmatchedTargetTokens()
{
    var links = TokenLinkBuilder.Build("Hello", "Xin chao");

    AssertEqual(2, links.Count);
    AssertEqual("Hello", links[0].SourceToken);
    AssertEqual("Xin", links[0].TargetToken);
    AssertEqual(string.Empty, links[1].SourceToken);
    AssertEqual("chao", links[1].TargetToken);
}

static void AssertContains(string actual, string expected)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected to find '{expected}' in '{actual}'.");
    }
}

static void AssertDoesNotContain(string actual, string unexpected)
{
    if (actual.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Did not expect to find '{unexpected}' in '{actual}'.");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}
