using LexVerse.Core.Geometry;

namespace LexVerse.Core.Overlay;

public sealed record TrackedOverlayItem(long Id, OverlayTextItem Item);

/// <summary>Session-local identities and physical-pixel jitter suppression. Not thread safe.</summary>
public sealed class OverlayItemTracker
{
    private List<TrackedOverlayItem> _items = [];
    private FrameGeometry? _geometry;
    private double _rotation;
    private long _nextId;
    private Guid _frameId;
    private bool _complete;

    public IReadOnlyList<TrackedOverlayItem> Update(OverlayRenderFrame frame, bool isPartial)
    {
        if (frame.Trace is null) { Clear(); return _items; }
        if (_geometry != frame.Trace.Geometry || _rotation != frame.RotationAngle)
        {
            _items.Clear();
            _geometry = frame.Trace.Geometry;
            _rotation = frame.RotationAngle;
            _frameId = Guid.Empty;
        }
        if (_frameId != frame.Trace.FrameId)
        {
            _frameId = frame.Trace.FrameId;
            _complete = false;
        }
        else if (_complete && isPartial) return _items;

        var matched = new HashSet<long>();
        var bySource = _items.GroupBy(entry => entry.Item.SourceText, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var next = new List<TrackedOverlayItem>();
        foreach (var item in frame.Items)
        {
            // Nearest one-to-one match also handles repeated text in different regions.
            TrackedOverlayItem? previous = null;
            var distance = double.PositiveInfinity;
            if (bySource.TryGetValue(item.SourceText, out var candidates))
                foreach (var candidate in candidates)
                {
                    if (matched.Contains(candidate.Id)) continue;
                    var candidateDistance = Distance(candidate.Item.TargetRect, item.TargetRect);
                    if (candidateDistance >= distance) continue;
                    previous = candidate;
                    distance = candidateDistance;
                }
            if (previous is null)
            {
                next.Add(new TrackedOverlayItem(++_nextId, item));
                continue;
            }
            matched.Add(previous.Id);
            var rect = previous.Item.TargetRect;
            var stable = Near(rect, item.TargetRect)
                ? item with { TargetRect = rect, SourceRect = previous.Item.SourceRect,
                    FontSize = previous.Item.FontSize }
                : item;
            next.Add(previous with { Item = stable });
        }

        // Retain visual order/instances even if OCR or translation completion order changes.
        var byId = next.ToDictionary(entry => entry.Id);
        var result = new List<TrackedOverlayItem>();
        foreach (var previous in _items)
        {
            if (byId.Remove(previous.Id, out var current)) result.Add(current);
            else if (isPartial) result.Add(previous);
        }
        result.AddRange(next.Where(entry => byId.ContainsKey(entry.Id)));
        _items = result;
        _complete = !isPartial;
        return _items;
    }

    public void Clear()
    {
        _items.Clear();
        _geometry = null;
        _frameId = Guid.Empty;
        _complete = false;
    }

    private static double Distance(ScreenRect a, ScreenRect b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) +
        Math.Abs(a.Width - b.Width) + Math.Abs(a.Height - b.Height);

    private static bool Near(ScreenRect a, ScreenRect b) =>
        Math.Abs(a.X - b.X) <= 2 && Math.Abs(a.Y - b.Y) <= 2 &&
        Math.Abs(a.Right - b.Right) <= 2 && Math.Abs(a.Bottom - b.Bottom) <= 2;
}
