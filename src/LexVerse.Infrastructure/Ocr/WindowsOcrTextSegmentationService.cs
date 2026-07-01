using LexVerse.Core.Models;
using LexVerse.Core.Services;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace LexVerse.Infrastructure.Ocr;

public sealed class WindowsOcrTextSegmentationService : ITextSegmentationService
{
    private const int MaxWordsPerFragment = 12;

    public async Task<IReadOnlyList<ScreenTextSegment>> DetectTextSegmentsAsync(
        ScreenSelection selection,
        CancellationToken cancellationToken = default)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            return [];
        }

        var pngBytes = ScreenCaptureService.CapturePngBytes(selection);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(pngBytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(bitmap);

        var segments = new List<ScreenTextSegment>();
        foreach (var line in result.Lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var words = line.Words
                .Where(word => !string.IsNullOrWhiteSpace(word.Text))
                .ToArray();
            if (words.Length == 0)
            {
                continue;
            }

            foreach (var fragment in SplitLineIntoFragments(words))
            {
                var text = string.Join(" ", fragment.Select(word => word.Text));
                var left = fragment.Min(word => word.BoundingRect.X);
                var top = fragment.Min(word => word.BoundingRect.Y);
                var right = fragment.Max(word => word.BoundingRect.X + word.BoundingRect.Width);
                var bottom = fragment.Max(word => word.BoundingRect.Y + word.BoundingRect.Height);

                segments.Add(new ScreenTextSegment(
                    segments.Count,
                    text,
                    new SourceBounds(
                        ClampUnit(left / Math.Max(1, selection.Width)),
                        ClampUnit(top / Math.Max(1, selection.Height)),
                        ClampUnit((right - left) / Math.Max(1, selection.Width)),
                        ClampUnit((bottom - top) / Math.Max(1, selection.Height)))));
            }
        }

        return segments;
    }

    private static IReadOnlyList<IReadOnlyList<OcrWord>> SplitLineIntoFragments(IReadOnlyList<OcrWord> words)
    {
        if (words.Count <= 1)
        {
            return [words];
        }

        var ordered = words
            .OrderBy(word => word.BoundingRect.X)
            .ToArray();
        var averageHeight = ordered.Average(word => word.BoundingRect.Height);
        var averageWordWidth = ordered.Average(word => word.BoundingRect.Width);
        var largeGapThreshold = Math.Max(averageHeight * 0.9, averageWordWidth * 0.65);

        var fragments = new List<IReadOnlyList<OcrWord>>();
        var current = new List<OcrWord> { ordered[0] };

        for (var index = 1; index < ordered.Length; index++)
        {
            var previous = ordered[index - 1];
            var word = ordered[index];
            var gap = word.BoundingRect.X - (previous.BoundingRect.X + previous.BoundingRect.Width);
            var shouldSplit = gap > largeGapThreshold
                || current.Count >= MaxWordsPerFragment
                || EndsFragment(previous.Text);

            if (shouldSplit)
            {
                fragments.Add(current.ToArray());
                current.Clear();
            }

            current.Add(word);
        }

        if (current.Count > 0)
        {
            fragments.Add(current.ToArray());
        }

        return fragments;
    }

    private static bool EndsFragment(string text)
    {
        return text.EndsWith('.') || text.EndsWith(';') || text.EndsWith(':') || text.EndsWith('?') || text.EndsWith('!');
    }

    private static double ClampUnit(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(1, value));
    }
}
