using System.Collections.ObjectModel;
using LexVerse.Core.Overlay;
using LexVerse.Overlay;
using LexVerse.Overlay.Models;
using Xunit;
using static LexVerse.Translation.Tests.OverlayItemTrackerTests;

namespace LexVerse.Translation.Tests;

public sealed class OverlayItemCollectionTests
{
    private static readonly OverlayRenderSettings Settings = new(1, 100, true, false);

    [Fact]
    public void UnchangedReorderedAndJitteringFramesDoNotMeasureOrMutateVisuals()
    {
        var items = new ObservableCollection<TextItem>();
        var collection = new OverlayItemCollection(items);
        var measurements = 0;
        TextItem Create(OverlayTextItem item) { measurements++; return Text(item); }
        var a = Item("a", 10);
        var b = Item("b", 200);
        collection.Update(Frame(a, b), false, Settings, Create, Text);
        var original = items.ToArray();
        var changes = 0;
        items.CollectionChanged += (_, _) => changes++;
        foreach (var item in items) item.PropertyChanged += (_, _) => changes++;
        for (var tick = 0; tick < 120; tick++)
        {
            var jitter = tick % 3 - 1;
            collection.Update(Frame(b with { TargetRect = b.TargetRect with { X = 200 + jitter } },
                a with { TargetRect = a.TargetRect with { Y = 20 + jitter } }), false, Settings, Create, Text);
        }
        Assert.Equal(2, measurements);
        Assert.Equal(0, changes);
        Assert.Same(original[0], items[0]);
        Assert.Same(original[1], items[1]);
    }

    [Fact]
    public void PartialsRetainPendingVisualsAndFinalReusesMatchingInstances()
    {
        var items = new ObservableCollection<TextItem>();
        var collection = new OverlayItemCollection(items);
        collection.Update(Frame(Item("a", 10), Item("b", 200)), false, Settings, Text, Text);
        var a = items[0];
        var b = items[1];
        var partial = Frame(Item("b", 200) with { TranslatedText = "updated" });
        collection.Update(partial, true, Settings, Text, Text);
        Assert.Equal(2, items.Count);
        Assert.Same(a, items[0]);
        Assert.Same(b, items[1]);
        Assert.Equal("updated", b.Text);
        collection.Update(partial, false, Settings, Text, Text);
        Assert.Same(b, Assert.Single(items));
        collection.Clear();
        Assert.Empty(items);
    }

    [Fact]
    public void SettingsChangesRebuildOnlyWhenNeededAndDebugCanBeToggled()
    {
        var items = new ObservableCollection<TextItem>();
        var collection = new OverlayItemCollection(items);
        var count = 0;
        TextItem Create(OverlayTextItem item) { count++; return Text(item); }
        var frame = Frame(Item("a", 10));
        collection.Update(frame, false, Settings, Create, Text);
        var original = items[0];
        collection.Update(frame, false, Settings with { FontScale = 150, PixelsPerDip = 1.5, Debug = true }, Create, Text);
        Assert.Equal(2, count);
        Assert.Equal(2, items.Count);
        Assert.Same(original, items[0]);
        collection.Update(frame, false, Settings, Create, Text);
        Assert.Same(original, Assert.Single(items));
    }

    private static TextItem Text(OverlayTextItem item) => new()
    {
        Text = item.TranslatedText, X = item.TargetRect.X, Y = item.TargetRect.Y,
        Width = item.TargetRect.Width, Height = item.TargetRect.Height, FontSize = item.FontSize
    };
}
