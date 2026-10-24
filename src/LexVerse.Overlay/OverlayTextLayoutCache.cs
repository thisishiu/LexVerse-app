namespace LexVerse.Overlay;

/// <summary>Bounded, UI-thread-owned cache; all measurement inputs are part of the key.</summary>
public sealed class OverlayTextLayoutCache(int capacity = 512)
{
    private readonly Dictionary<LayoutKey, OverlayTextLayout> _entries = [];
    private readonly Queue<LayoutKey> _order = new();
    public int Count => _entries.Count;
    public int MeasurementCount { get; private set; }

    public OverlayTextLayout Calculate(string text, double width, double height, double maximumWidth,
        double fontSize, double minimumFontSize, double horizontalPadding, double verticalPadding, double pixelsPerDip)
    {
        var key = new LayoutKey(text, width, height, maximumWidth, fontSize, minimumFontSize,
            horizontalPadding, verticalPadding, pixelsPerDip, System.Globalization.CultureInfo.CurrentUICulture.Name);
        if (_entries.TryGetValue(key, out var layout)) return layout;
        layout = OverlayTextLayoutCalculator.Calculate(text, width, height, maximumWidth, fontSize,
            minimumFontSize, horizontalPadding, verticalPadding, pixelsPerDip);
        MeasurementCount++;
        if (capacity <= 0) return layout;
        if (_entries.Count >= capacity) _entries.Remove(_order.Dequeue());
        _entries.Add(key, layout);
        _order.Enqueue(key);
        return layout;
    }

    public void Clear() { _entries.Clear(); _order.Clear(); }

    private readonly record struct LayoutKey(string Text, double Width, double Height, double MaximumWidth,
        double FontSize, double MinimumFontSize, double HorizontalPadding, double VerticalPadding, double PixelsPerDip,
        string Culture);
}
