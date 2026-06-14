using System.Collections.Generic;

namespace LexVerse.Overlay.Services
{
    public enum OverlayStatus
    {
        Stopped,
        Starting,
        Running,
        Hidden,
        Error
    }

    public interface IOverlayService
    {
        OverlayStatus Status { get; }

        event EventHandler<OverlayStatus>? StatusChanged;

        void Start();
        void Stop();

        void Show();
        void Hide();

        void UpdateItems(IEnumerable<Models.OverlayTextItem> items);
        void Clear();

        void ToggleClickThrough(bool enable);
    }
}
