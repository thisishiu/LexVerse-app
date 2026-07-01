using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using LexVerse.Core.Models;

namespace LexVerse.App;

public partial class SourceHighlightOverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExNoactivate = 0x08000000;

    private readonly ScreenSelection _selection;

    public SourceHighlightOverlayWindow(ScreenSelection selection)
    {
        InitializeComponent();
        _selection = selection;

        Left = selection.X;
        Top = selection.Y;
        Width = selection.Width;
        Height = selection.Height;
        SourceInitialized += SourceHighlightOverlayWindow_SourceInitialized;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public void ShowHighlight(SourceBounds? bounds)
    {
        EnsureVisible();
        HighlightCanvas.Children.Clear();
        if (bounds is null)
        {
            return;
        }

        var rectangle = new Rectangle
        {
            Width = Math.Max(1, bounds.Width * _selection.Width),
            Height = Math.Max(1, bounds.Height * _selection.Height),
            RadiusX = 6,
            RadiusY = 6,
            Fill = new SolidColorBrush(Color.FromArgb(72, 255, 214, 128)),
            Stroke = new SolidColorBrush(Color.FromArgb(230, 255, 197, 91)),
            StrokeThickness = 2
        };

        Canvas.SetLeft(rectangle, Math.Max(0, Math.Min(_selection.Width, bounds.X * _selection.Width)));
        Canvas.SetTop(rectangle, Math.Max(0, Math.Min(_selection.Height, bounds.Y * _selection.Height)));
        HighlightCanvas.Children.Add(rectangle);
    }

    public void ShowWholeSelectionHint()
    {
        ShowHighlight(new SourceBounds(0, 0, 1, 1));
    }

    public void ClearHighlight()
    {
        HighlightCanvas.Children.Clear();
    }

    private void SourceHighlightOverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, GwlExstyle);
        SetWindowLong(handle, GwlExstyle, style | WsExTransparent | WsExToolwindow | WsExNoactivate);
    }

    private void EnsureVisible()
    {
        if (!IsVisible)
        {
            Show();
        }

        // Re-assert topmost so the highlight stays above the captured app but below the user's attention on the popup.
        Topmost = false;
        Topmost = true;
    }
}
