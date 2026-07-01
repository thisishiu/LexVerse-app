using System.Runtime.InteropServices;
using System.Windows;
using LexVerse.Core.Models;

namespace LexVerse.App;

public partial class RealtimeOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly ScreenSelection _selection;

    public RealtimeOverlayWindow(ScreenSelection selection)
    {
        _selection = selection;
        InitializeComponent();
        Loaded += RealtimeOverlayWindow_Loaded;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public void UpdateText(string text, double opacity, double fontSize)
    {
        Shell.Opacity = Math.Clamp(opacity, 0.45, 1);
        TranslationText.FontSize = Math.Clamp(fontSize, 11, 30);
        TranslationText.LineHeight = Math.Clamp(fontSize * 1.35, 16, 42);
        TranslationText.Text = string.IsNullOrWhiteSpace(text) ? "..." : text.Trim();
        PulseText.Text = DateTime.Now.ToString("HH:mm:ss");
        PositionNearSelection();
    }

    public void ShowReading()
    {
        PulseText.Text = "reading";
    }

    private void RealtimeOverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var style = GetWindowLong(helper.Handle, GwlExStyle);
        SetWindowLong(helper.Handle, GwlExStyle, style | WsExTransparent | WsExToolWindow | WsExNoActivate);
        PositionNearSelection();
    }

    private void PositionNearSelection()
    {
        var workArea = SystemParameters.WorkArea;
        var desiredLeft = (double)_selection.X;
        var desiredTop = _selection.Y + _selection.Height + 10d;

        if (desiredTop + ActualHeight > workArea.Bottom)
        {
            desiredTop = _selection.Y - ActualHeight - 10d;
        }

        if (desiredTop < workArea.Top)
        {
            desiredTop = workArea.Top + 10;
        }

        if (desiredLeft + ActualWidth > workArea.Right)
        {
            desiredLeft = workArea.Right - ActualWidth - 10;
        }

        Left = Math.Max(workArea.Left + 10, desiredLeft);
        Top = Math.Max(workArea.Top + 10, desiredTop);
    }
}
