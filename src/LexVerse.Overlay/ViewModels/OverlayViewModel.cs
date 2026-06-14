using System.Collections.ObjectModel;
using LexVerse.Overlay.Models;

namespace LexVerse.Overlay.ViewModels
{
    public class OverlayViewModel
    {
        public ObservableCollection<TextItem> Items { get; } = new ObservableCollection<TextItem>();
    }
}
