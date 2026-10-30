using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;

namespace LexVerse.App;

public partial class MainWindow
{
    private bool _startupChromeRestored;

    private void RestoreStartupChrome()
    {
        if (_startupChromeRestored) return;
        _startupChromeRestored = true;
        CaptionRow.Height = new GridLength(36);
        CaptionBar.Visibility = Visibility.Visible;
        // Keep WindowStyle/ResizeMode fixed for the lifetime of the HWND. Changing
        // native window styles here can bring back the system caption buttons
        // alongside our custom caption during a non-client frame refresh.
        var chrome = WindowChrome.GetWindowChrome(this);
        chrome.CaptionHeight = 36;
        chrome.ResizeBorderThickness = new Thickness(6);
        Background = (Brush)FindResource("BackgroundBrush");
        UpdateChromePadding();
    }

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
        // WebView2 owns a child HWND: keep it away from the resize hit-test band.
        // Maximized bounds already match the monitor work area (see below).
        ChromeFrame.Padding = WindowState == WindowState.Maximized
            ? new Thickness(0)
            : new Thickness(6);
    }

    private void ApplyMaximizedWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
        var info = new ChromeMonitorInfo { Size = Marshal.SizeOf<ChromeMonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;

        // Native coordinates are physical pixels, including on mixed-DPI monitors.
        var bounds = Marshal.PtrToStructure<ChromeMinMaxInfo>(lParam);
        bounds.MaxPosition.X = info.Work.Left - info.Monitor.Left;
        bounds.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
        bounds.MaxSize.X = info.Work.Right - info.Work.Left;
        bounds.MaxSize.Y = info.Work.Bottom - info.Work.Top;
        // This message is handled by us, so WPF cannot apply MinWidth/MinHeight.
        // Enforce those limits in native pixels to prevent clipping the caption
        // buttons and WebView when the user drags a resize edge.
        var dpi = VisualTreeHelper.GetDpi(this);
        bounds.MinTrackSize.X = Math.Max(bounds.MinTrackSize.X,
            (int)Math.Ceiling(MinWidth * dpi.DpiScaleX));
        bounds.MinTrackSize.Y = Math.Max(bounds.MinTrackSize.Y,
            (int)Math.Ceiling(MinHeight * dpi.DpiScaleY));
        Marshal.StructureToPtr(bounds, lParam, false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ChromePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ChromeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ChromeMinMaxInfo
    {
        public ChromePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ChromeMonitorInfo
    {
        public int Size;
        public ChromeRect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref ChromeMonitorInfo info);

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
