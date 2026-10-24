using LexVerse.Core.Capture;
using LexVerse.Core.Geometry;
using LexVerse.Core.Imaging;
using LexVerse.Core.Ocr;
using LexVerse.Core.Pipeline;
using LexVerse.Core.ScreenCapture;
using LexVerse.Core.Translation;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class RealtimeTranslationPipelineCacheTests
{
    [Fact]
    public async Task SamePixelsWithChangedGeometryDoNotReuseOcr()
    {
        var first = CreateFrame();
        var second = CreateFrame() with { Geometry = first.Geometry with { DpiScaleX = 1.5, Version = 1 } };
        var ocr = new FakeOcrService();
        var pipeline = new RealtimeTranslationPipeline(new FakeCaptureSession(first, second),
            new ExactFrameChangeDetector(), ocr, new FakeTextTranslator(), new InMemoryTranslationCache(),
            RealtimeTranslationOptions.Document);
        await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);
        var result = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, ocr.CallCount);
        Assert.False(result.Ocr.UsedCachedOcrResult);
    }

    [Fact]
    public async Task SamePixelsWithChangedRegionReevaluateOcrInput()
    {
        var regions = new MutableRegions();
        var ocr = new FakeOcrService();
        var pipeline = new RealtimeTranslationPipeline(new FakeCaptureSession(CreateFrame(), CreateFrame()),
            new ExactFrameChangeDetector(), ocr, new FakeTextTranslator(), new InMemoryTranslationCache(),
            RealtimeTranslationOptions.Document, regions);
        await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);
        regions.X = 8;
        var result = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, ocr.CallCount);
        Assert.False(result.Ocr.UsedCachedOcrResult);
    }

    [Fact]
    public async Task RepeatedFramesKeepOcrAndTranslationCallsConstant()
    {
        var ocr = new FakeOcrService();
        var translator = new FakeTextTranslator();
        var pipeline = new RealtimeTranslationPipeline(new FakeCaptureSession(
                Enumerable.Range(0, 120).Select(_ => CreateFrame()).ToArray()),
            new ExactFrameChangeDetector(), ocr, translator, new InMemoryTranslationCache(),
            RealtimeTranslationOptions.Document);
        for (var index = 0; index < 120; index++)
            await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, ocr.CallCount);
        Assert.Equal(1, translator.CallCount);
    }

    private sealed class MutableRegions : IOcrRegionProvider
    {
        public int X { get; set; }
        public IReadOnlyList<OcrRegion> GetRegions(CapturedFrame frame) =>
            [new OcrRegion("test", X, 0, 4, 4, OcrProcessingMode.Document)];
    }

    [Theory]
    [InlineData(OcrProcessingMode.FullScreen)]
    [InlineData(OcrProcessingMode.Document)]
    [InlineData(OcrProcessingMode.GameDialogue)]
    [InlineData(OcrProcessingMode.Subtitle)]
    [InlineData(OcrProcessingMode.Comic)]
    public async Task WithManyBlocks_TranslatesEveryBlockAndCachesCompleteFrame(OcrProcessingMode mode)
    {
        const int expectedCount = 240;
        var blocks = Enumerable.Range(0, expectedCount).Select(index =>
        {
            var bounds = new BoundingBox(1, index * 20, 100, 15);
            var text = $"hello block {index}";
            return new OcrTextBlock(text, bounds, [new OcrWord(text, bounds, 12)], 12);
        }).ToArray();
        var ocrService = new FakeOcrService(blocks);
        var translator = new FakeTextTranslator();
        var pipeline = new RealtimeTranslationPipeline(
            new FakeCaptureSession(CreateFrame(200, expectedCount * 20), CreateFrame(200, expectedCount * 20)),
            new ExactFrameChangeDetector(), ocrService, translator,
            new InMemoryTranslationCache(),
            RealtimeTranslationOptions.FullScreen with { Mode = mode });
        var partials = new List<RealtimeTranslationPipelineResult>();
        var result = await pipeline.CaptureRecognizeAndTranslateAsync(
            TestContext.Current.CancellationToken,
            (partial, _) => { partials.Add(partial); return Task.CompletedTask; });
        var cached = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedCount, result.TranslatedBlocks.Count);
        Assert.Equal(expectedCount, result.OverlayFrame.Items.Count);
        Assert.Equal(expectedCount, translator.CallCount);
        Assert.Contains(partials, partial => partial.TranslatedBlocks.Count == 16);
        Assert.Equal(expectedCount, partials[^1].OverlayFrame.Items.Count);
        Assert.Equal(Enumerable.Range(1, expectedCount / 16).Select(batch => batch * 16),
            partials.Select(partial => partial.TranslatedBlocks.Count));
        Assert.All(partials, partial => Assert.Equal(
            blocks.Take(partial.TranslatedBlocks.Count).Select(block => block.Text),
            partial.TranslatedBlocks.Select(block => block.SourceText)));
        var lastText = $"hello block {expectedCount - 1}";
        Assert.Contains(result.OverlayFrame.Items, item => item.SourceText == lastText);
        Assert.Equal((expectedCount - 1) * 20, result.TranslatedBlocks[^1].Bounds.Y);
        Assert.True(cached.UsedCachedTranslation);
        Assert.Equal(1, ocrService.CallCount);
        Assert.Equal(expectedCount, cached.OverlayFrame.Items.Count);
        Assert.Contains(cached.OverlayFrame.Items, item => item.SourceText == lastText);
    }

    [Fact]
    public async Task CaptureRecognizeAndTranslateAsync_WhenFrameRepeats_ReusesOcrAndTranslation()
    {
        var firstFrame = CreateFrame();
        var repeatedFrame = CreateFrame();
        var captureSession = new FakeCaptureSession(firstFrame, repeatedFrame);
        var ocrService = new FakeOcrService();
        var translator = new FakeTextTranslator();
        var pipeline = new RealtimeTranslationPipeline(
            captureSession,
            new ExactFrameChangeDetector(),
            ocrService,
            translator,
            new InMemoryTranslationCache(),
            RealtimeTranslationOptions.GameDialogue);

        var firstResult = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);
        var secondResult = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, ocrService.CallCount);
        Assert.Equal(1, translator.CallCount);
        Assert.True(secondResult.Ocr.UsedCachedOcrResult);
        Assert.True(secondResult.UsedCachedTranslation);
        Assert.False(secondResult.Ocr.Changed);
        Assert.Single(firstResult.OverlayFrame.Items);
        Assert.Single(secondResult.OverlayFrame.Items);
        Assert.Equal("xin chao", secondResult.OverlayFrame.Items[0].TranslatedText);
    }

    [Fact]
    public async Task CaptureRecognizeAndTranslateAsync_WhenPromptChanges_RetranslatesCachedOcr()
    {
        var firstFrame = CreateFrame();
        var repeatedFrame = CreateFrame();
        var captureSession = new FakeCaptureSession(firstFrame, repeatedFrame);
        var ocrService = new FakeOcrService();
        var translator = new FakeTextTranslator();
        var pipeline = new RealtimeTranslationPipeline(
            captureSession,
            new ExactFrameChangeDetector(),
            ocrService,
            translator,
            new InMemoryTranslationCache(),
            RealtimeTranslationOptions.Document with
            {
                TranslationPrompt = new TranslationPromptOptions("natural style")
            });

        _ = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);
        pipeline.UpdateTranslationPrompt(new TranslationPromptOptions("literal style"));
        var secondResult = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, ocrService.CallCount);
        Assert.Equal(2, translator.CallCount);
        Assert.True(secondResult.Ocr.UsedCachedOcrResult);
        Assert.False(secondResult.UsedCachedTranslation);
        Assert.Equal("xin chao:literal style", secondResult.OverlayFrame.Items[0].TranslatedText);
        Assert.NotEqual(translator.PromptKeys[0], translator.PromptKeys[1]);
    }

    [Fact]
    public async Task CaptureRecognizeAndTranslateAsync_WithIgnoredGameStats_SuppressesIgnoredBlocks()
    {
        var captureSession = new FakeCaptureSession(CreateFrame());
        var ocrService = new FakeOcrService(
            CreateTextBlock("HP", 1),
            CreateTextBlock("FPS 60", 5),
            CreateTextBlock("hello", 9));
        var translator = new FakeTextTranslator();
        var pipeline = new RealtimeTranslationPipeline(
            captureSession,
            new ExactFrameChangeDetector(),
            ocrService,
            translator,
            new InMemoryTranslationCache(),
            RealtimeTranslationOptions.Document with
            {
                TranslationPrompt = new TranslationPromptOptions(
                    ignoredTerms: ["HP", "MP", "EXP", "FPS"],
                    preserveAcronymsAndTechnicalTerms: true)
            });

        var result = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, translator.CallCount);
        Assert.Single(result.TranslatedBlocks);
        Assert.Single(result.OverlayFrame.Items);
        Assert.Equal("hello", result.TranslatedBlocks[0].SourceText);
    }

    [Fact]
    public async Task CaptureRecognizeAndTranslateAsync_WhenOcrDetectsTextAngle_PropagatesOverlayRotation()
    {
        var captureSession = new FakeCaptureSession(CreateFrame());
        var ocrService = new FakeOcrService
        {
            TextAngle = 2.75
        };
        var pipeline = new RealtimeTranslationPipeline(
            captureSession,
            new ExactFrameChangeDetector(),
            ocrService,
            new FakeTextTranslator(),
            new InMemoryTranslationCache(),
            RealtimeTranslationOptions.Document);

        var result = await pipeline.CaptureRecognizeAndTranslateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2.75, result.OverlayFrame.RotationAngle);
    }

    private static CapturedFrame CreateFrame(int width = 16, int height = 8)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];

        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 32;
            pixels[index + 1] = 64;
            pixels[index + 2] = 128;
            pixels[index + 3] = 255;
        }

        return new CapturedFrame(
            width,
            height,
            stride,
            PixelFormat.Bgra8,
            pixels,
            DateTimeOffset.UtcNow,
            new FrameGeometry(
                new PixelSize(width, height),
                new ScreenRect(0, 0, width, height),
                CoordinateSpace.FrameLocal,
                1,
                1,
                0),
            new CaptureSourceInfo(CaptureSourceKind.Picker, "test"));
    }

    private static OcrTextBlock CreateTextBlock(string text, int x)
    {
        var bounds = new BoundingBox(x, 1, 4, 3);
        var word = new OcrWord(text, bounds, 12);
        return new OcrTextBlock(text, bounds, [word], 12);
    }

    private sealed class FakeCaptureSession(params CapturedFrame[] frames) : IScreenCaptureSession
    {
        private readonly Queue<CapturedFrame> _frames = new(frames);

        public Task<CapturedFrame> CaptureFrameAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_frames.Dequeue());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeOcrService(params OcrTextBlock[] blocks) : IOcrService
    {
        public int CallCount { get; private set; }

        public double TextAngle { get; init; }

        public Task<OcrResult> RecognizeAsync(CapturedFrame frame, CancellationToken cancellationToken = default)
        {
            CallCount++;
            var recognizedBlocks = blocks.Length > 0
                ? blocks
                : [CreateTextBlock("hello", 1)];
            var result = new OcrResult(
                recognizedBlocks,
                "en-US",
                TextAngle,
                DateTimeOffset.UtcNow);

            return Task.FromResult(result);
        }
    }

    private sealed class FakeTextTranslator : ITextTranslator
    {
        public int CallCount { get; private set; }
        public List<string> PromptKeys { get; } = [];

        public Task<TextTranslationResult> TranslateAsync(
            string text,
            string targetLanguage,
            string? sourceLanguage = null,
            CancellationToken cancellationToken = default)
        {
            return TranslateAsync(
                text,
                targetLanguage,
                sourceLanguage,
                TranslationPromptOptions.Empty,
                cancellationToken);
        }

        public Task<TextTranslationResult> TranslateAsync(
            string text,
            string targetLanguage,
            string? sourceLanguage,
            TranslationPromptOptions? promptOptions,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            promptOptions ??= TranslationPromptOptions.Empty;
            PromptKeys.Add(promptOptions.CacheKey);

            var translatedText = promptOptions.HasInstruction
                ? $"xin chao:{promptOptions.Instruction}"
                : "xin chao";

            return Task.FromResult(new TextTranslationResult(text, translatedText, targetLanguage, sourceLanguage));
        }
    }
}
