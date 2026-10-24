using LexVerse.Core.Geometry;
using LexVerse.Core.Overlay;
using LexVerse.Core.Pipeline;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OverlayItemTrackerTests
{
    [Fact]
    public void ReorderingAndTwoPixelJitterKeepIdentityOrderBoundsAndFont()
    {
        var tracker = new OverlayItemTracker();
        var a = Item("a", 10);
        var b = Item("b", 200);
        var first = tracker.Update(Frame(a, b), false).ToArray();
        var second = tracker.Update(Frame(b with { TargetRect = new(202, 22, 100, 30), FontSize = 19 },
            a with { TargetRect = new(8, 18, 100, 30), FontSize = 17 }), false);
        Assert.Equal(first.Select(x => x.Id), second.Select(x => x.Id));
        Assert.Equal(first.Select(x => x.Item.TargetRect), second.Select(x => x.Item.TargetRect));
        Assert.All(second, entry => Assert.Equal(16, entry.Item.FontSize));
    }

    [Fact]
    public void JitterDoesNotAccumulateAndLargeMovementUpdatesImmediately()
    {
        var tracker = new OverlayItemTracker();
        var first = tracker.Update(Frame(Item("a", 10)), false).Single();
        tracker.Update(Frame(Item("a", 12)), false);
        var moved = tracker.Update(Frame(Item("a", 13)), false).Single();
        Assert.Equal(first.Id, moved.Id);
        Assert.Equal(13, moved.Item.TargetRect.X);
    }

    [Fact]
    public void PartialUpdatesKeepPendingBoxesAndFinalRemovesMissingOnes()
    {
        var tracker = new OverlayItemTracker();
        var initial = tracker.Update(Frame(Item("a", 10), Item("b", 200), Item("gone", 350)), false).ToArray();
        var frame = Frame(Item("b", 200) with { TranslatedText = "new b" });
        var partial = tracker.Update(frame, true).ToArray();
        Assert.Equal(3, partial.Length);
        Assert.Equal(initial[0], partial[0]);
        Assert.Equal("new b", partial[1].Item.TranslatedText);
        var final = tracker.Update(frame with { Items = [Item("a", 10), frame.Items[0]] }, false);
        Assert.Equal(initial.Take(2).Select(x => x.Id), final.Select(x => x.Id));
        // Late partial from the completed frame cannot resurrect removed boxes.
        Assert.Equal(2, tracker.Update(frame with { Items = [Item("gone", 350)] }, true).Count);
    }

    [Fact]
    public void DuplicateTextUsesNearestRegionWithoutSharingIdentity()
    {
        var tracker = new OverlayItemTracker();
        var first = tracker.Update(Frame(Item("same", 10), Item("same", 200)), false).ToArray();
        var next = tracker.Update(Frame(Item("same", 201), Item("same", 9)), false);
        Assert.Equal(first.Select(x => x.Id), next.Select(x => x.Id));
        Assert.Equal(first.Select(x => x.Item.TargetRect), next.Select(x => x.Item.TargetRect));
    }

    [Fact]
    public void ContentChangeAndEmptyFinalReplaceOldBoxes()
    {
        var tracker = new OverlayItemTracker();
        var old = tracker.Update(Frame(Item("old", 10)), false).Single();
        var next = tracker.Update(Frame(Item("new", 10)), false).Single();
        Assert.NotEqual(old.Id, next.Id);
        Assert.Empty(tracker.Update(Frame(), false));
    }

    [Fact]
    public void GeometryAndSessionResetDoNotKeepOldCoordinates()
    {
        var tracker = new OverlayItemTracker();
        var first = tracker.Update(Frame(Item("a", 10)), false).Single();
        var frame = Frame(Item("a", 11));
        var changed = frame with { Trace = frame.Trace! with { Geometry = frame.Trace.Geometry with { DpiScaleX = 1.5 } } };
        var next = tracker.Update(changed, true).Single();
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(11, next.Item.TargetRect.X);
        tracker.Clear();
        Assert.Empty(tracker.Update(Frame(), true));
    }

    internal static OverlayTextItem Item(string text, double x) => new(text, "translated " + text,
        new ScreenRect(x, 20, 100, 30), 1, 16);

    internal static OverlayRenderFrame Frame(params OverlayTextItem[] items) => new(items,
        new PipelineTrace(Guid.NewGuid(), "test", FrameGeometry.FrameLocal(new PixelSize(800, 600)),
            TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
            items.Length, 0, 0, 0, false, null), 0, DateTimeOffset.UtcNow);
}
