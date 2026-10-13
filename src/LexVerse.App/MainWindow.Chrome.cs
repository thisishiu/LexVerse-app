using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace LexVerse.App;

public partial class MainWindow
{
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateChromePadding();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateChromePadding();
    }

    private void UpdateChromePadding()
    {
        if (ChromeFrame is null) return;
        if (WindowState != WindowState.Maximized)
        {
            ChromeFrame.Padding = new Thickness(0);
            return;
        }

        // Maximized native windows extend their resize frame past the work area.
        // Inset client content by that frame, in DIPs for the current monitor.
        var scale = VisualTreeHelper.GetDpi(this);
        var dpi = (uint)Math.Round(96 * scale.DpiScaleX);
        var paddedBorder = GetSystemMetricsForDpi(92, dpi); // SM_CXPADDEDBORDER
        var horizontal = (GetSystemMetricsForDpi(32, dpi) + paddedBorder) / scale.DpiScaleX;
        var vertical = (GetSystemMetricsForDpi(33, dpi) + paddedBorder) / scale.DpiScaleY;
        ChromeFrame.Padding = new Thickness(horizontal, vertical, horizontal, vertical);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    private void CaptionMinimize_Click(object sender, RoutedEventArgs e) =>
        SystemCommands.MinimizeWindow(this);

    private void CaptionMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    // Close follows the existing asynchronous background-work cleanup path.
    private void CaptionClose_Click(object sender, RoutedEventArgs e) => Close();
}
