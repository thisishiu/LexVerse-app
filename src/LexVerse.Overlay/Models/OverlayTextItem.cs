using System;

namespace LexVerse.Overlay.Models
{
    // Coordinates are in physical pixels by default; controller will convert to WPF DIP.
    public class OverlayTextItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Text { get; set; } = string.Empty;

        // Physical pixel coordinates relative to monitor specified by ScreenId
        public double X { get; set; }
        public double Y { get; set; }

        // Optional target monitor identifier (DeviceName or index). Null = primary
        public string? ScreenId { get; set; }

        public double FontSize { get; set; } = 20;
        public double MaxWidth { get; set; } = 600; // pixels
        public bool Wrap { get; set; } = true;

        // Styling hints
        public string? Foreground { get; set; }
        public string? Background { get; set; }
    }
}
