using System.Collections.ObjectModel;
using LexVerse.Core.Geometry;
using LexVerse.Core.Overlay;
using LexVerse.Overlay.Models;

namespace LexVerse.Overlay;

public sealed record OverlayRenderSettings(double PixelsPerDip, int FontScale, bool Dark, bool Debug);

/// <summary>Reconciles persistent WPF items without rebuilding unchanged text or disturbing visual order.</summary>
public sealed class OverlayItemCollection(ObservableCollection<TextItem> items)
{
    private readonly OverlayItemTracker _tracker = new();
    private readonly Dictionary<long, RenderedItem> _rendered = [];

    public void Update(OverlayRenderFrame frame, bool isPartial, OverlayRenderSettings settings,
        Func<OverlayTextItem, TextItem> createText, Func<OverlayTextItem, TextItem> createDebug)
    {
        var tracked = _tracker.Update(frame, isPartial);
        var liveIds = tracked.Select(entry => entry.Id).ToHashSet();
        foreach (var id in _rendered.Keys.Where(id => !liveIds.Contains(id)).ToArray())
        {
            var removed = _rendered[id];
            items.Remove(removed.Text);
            if (removed.Debug is not null) items.Remove(removed.Debug);
            _rendered.Remove(id);
        }
        foreach (var entry in tracked)
        {
            var item = entry.Item;
            var key = new RenderKey(item.TranslatedText, item.TargetRect, item.FontSize,
                frame.Trace!.Geometry, settings);
            if (_rendered.TryGetValue(entry.Id, out var existing) && existing.Key == key) continue;
            var text = createText(item);
            var debug = settings.Debug ? createDebug(item) : null;
            if (existing is not null)
            {
                Copy(text, existing.Text);
                text = existing.Text;
                if (existing.Debug is not null)
                {
                    if (debug is null) items.Remove(existing.Debug);
                    else { Copy(debug, existing.Debug); debug = existing.Debug; }
                }
            }
            SetVisible(text);
            if (debug is not null) SetVisible(debug);
            _rendered[entry.Id] = new RenderedItem(key, text, debug);
        }
    }

    public void Clear() { items.Clear(); _tracker.Clear(); _rendered.Clear(); }

    private void SetVisible(TextItem item)
    {
        if (item.Width > 0 && item.Height > 0)
        {
            if (!items.Contains(item)) items.Add(item);
        }
        else items.Remove(item);
    }

    private static void Copy(TextItem source, TextItem target)
    {
        target.Text = source.Text;
        target.X = source.X;
        target.Y = source.Y;
        target.Width = source.Width;
        target.Height = source.Height;
        target.MinHeight = source.MinHeight;
        target.FontSize = source.FontSize;
        target.Background = source.Background;
        target.BorderBrush = source.BorderBrush;
        target.Foreground = source.Foreground;
    }

    private sealed record RenderedItem(RenderKey Key, TextItem Text, TextItem? Debug);
    private sealed record RenderKey(string Text, ScreenRect Rect, double FontSize,
        FrameGeometry Geometry, OverlayRenderSettings Settings);
}
