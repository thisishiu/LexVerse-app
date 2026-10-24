using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using LexVerse.Core.Geometry;
using LexVerse.Core.Overlay;
using LexVerse.Core.Pipeline;
using LexVerse.Overlay;
using LexVerse.Overlay.Models;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        const int frameCount = 120;
        var blocks = Enumerable.Range(0, 80).Select(index => new OverlayTextItem($"source {index}",
            $"Translated paragraph {index}: repeated text wrapping inside a fixed source rectangle.",
            new ScreenRect(index % 8 * 220, index / 8 * 45, 200, 36), 1, 16)).ToArray();
        var geometry = FrameGeometry.FrameLocal(new PixelSize(1920, 1080));
        OverlayRenderFrame Frame() => new(blocks, new PipelineTrace(Guid.NewGuid(), "synthetic", geometry,
            TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
            blocks.Length, 0, 0, 0, false, null), 0, DateTimeOffset.UtcNow);

        TextItem Create(OverlayTextItem item, OverlayTextLayoutCache? cache)
        {
            var layout = cache is null
                ? OverlayTextLayoutCalculator.Calculate(item.TranslatedText, 200, 36, 200, 16, 8, 4, 2, 1)
                : cache.Calculate(item.TranslatedText, 200, 36, 200, 16, 8, 4, 2, 1);
            return new TextItem { Text = item.TranslatedText, X = item.TargetRect.X, Y = item.TargetRect.Y,
                Width = layout.Width, Height = layout.Height, MinHeight = layout.Height, FontSize = layout.FontSize };
        }

        // Warm the formatter, JIT and both paths before collecting process-local measurements.
        foreach (var block in blocks) Create(block, null);
        var warm = new OverlayItemCollection([]);
        warm.Update(Frame(), false, new(1, 100, true, false), item => Create(item, null), item => Create(item, null));

        var legacy = new ObservableCollection<TextItem>();
        var baseline = Measure(() =>
        {
            for (var frame = 0; frame < frameCount; frame++)
            {
                // Previous output rebuilt/measured every item, then copied by list index.
                var next = blocks.Select(block => Create(block, null)).ToArray();
                for (var index = 0; index < next.Length; index++)
                {
                    if (index >= legacy.Count) { legacy.Add(next[index]); continue; }
                    var source = next[index];
                    var target = legacy[index];
                    target.Text = source.Text; target.X = source.X; target.Y = source.Y;
                    target.Width = source.Width; target.Height = source.Height;
                    target.MinHeight = source.MinHeight; target.FontSize = source.FontSize;
                }
            }
        });

        var visuals = new ObservableCollection<TextItem>();
        var optimized = new OverlayItemCollection(visuals);
        var cache = new OverlayTextLayoutCache();
        var collectionChanges = 0;
        var propertyChanges = 0;
        optimized.Update(Frame(), false, new(1, 100, true, false), item => Create(item, cache), item => Create(item, cache));
        visuals.CollectionChanged += (_, _) => collectionChanges++;
        foreach (var visual in visuals) visual.PropertyChanged += (_, _) => propertyChanges++;
        var updated = Measure(() =>
        {
            for (var frame = 0; frame < frameCount; frame++)
                optimized.Update(Frame(), false, new(1, 100, true, false),
                    item => Create(item, cache), item => Create(item, cache));
        });
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            scenario = "Synthetic static overlay only; excludes capture, OCR, network, native window and WebView",
            frameCount, boxCount = blocks.Length, baseline, optimized = updated,
            baselineLayoutCalls = frameCount * blocks.Length,
            optimizedLayoutCallsAfterFirstFrame = cache.MeasurementCount - blocks.Length,
            collectionChangesAfterFirstFrame = collectionChanges,
            propertyChangesAfterFirstFrame = propertyChanges
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Sample Measure(Action action)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        action();
        timer.Stop();
        return new Sample(timer.Elapsed.TotalMilliseconds, (process.TotalProcessorTime - cpu).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocated);
    }

    private sealed record Sample(double ElapsedMs, double CpuMs, long AllocatedBytes);
}
