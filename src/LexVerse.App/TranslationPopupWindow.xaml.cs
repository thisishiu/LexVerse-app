using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LexVerse.App;

public partial class TranslationPopupWindow : Window
{
    private const int VkLeftButton = 0x01;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private readonly bool _closeOnOutsideClick;
    private readonly bool _allowDragging;
    private readonly DispatcherTimer _outsideClickTimer;
    private bool _wasLeftButtonPressed;

    public TranslationPopupWindow(
        bool closeOnOutsideClick = true,
        bool allowDragging = true)
    {
        InitializeComponent();
        _closeOnOutsideClick = closeOnOutsideClick;
        _allowDragging = allowDragging;
        _outsideClickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _outsideClickTimer.Tick += OutsideClickTimer_Tick;
        PositionNearCursor();
    }

    public void ShowResult(string sourceText, string translatedText, string targetLabel)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Visible;
        SourceTextBlock.Text = sourceText;
        TranslatedTextBlock.Text = translatedText;
        TargetLabel.Text = targetLabel;
        RepositionInsideVirtualScreen();
    }

    public void ShowError(string title, string message)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        ErrorTitle.Text = title;
        ErrorMessage.Text = message;
        RepositionInsideVirtualScreen();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TranslatedTextBlock.Text);
            CopyButton.Content = "Copied";
        }
        catch
        {
            CopyButton.Content = "Copy failed";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_allowDragging && e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RepositionInsideVirtualScreen();
        if (_closeOnOutsideClick)
        {
            _outsideClickTimer.Start();
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    protected override void OnClosed(EventArgs e)
    {
        _outsideClickTimer.Stop();
        _outsideClickTimer.Tick -= OutsideClickTimer_Tick;
        base.OnClosed(e);
    }

    private void OutsideClickTimer_Tick(object? sender, EventArgs e)
    {
        var isPressed = (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
        if (!isPressed)
        {
            _wasLeftButtonPressed = false;
            return;
        }

        if (_wasLeftButtonPressed)
        {
            return;
        }

        _wasLeftButtonPressed = true;
        if (GetCursorPos(out var point))
        {
            var localPoint = PointFromScreen(new Point(point.X, point.Y));
            if (localPoint.X < 0 || localPoint.Y < 0 ||
                localPoint.X > ActualWidth || localPoint.Y > ActualHeight)
            {
                Close();
            }
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _ = SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, WdaExcludeFromCapture);
    }

    private void PositionNearCursor()
    {
        if (GetCursorPos(out var point))
        {
            Left = point.X + 18;
            Top = point.Y + 18;
            return;
        }

        Left = SystemParameters.WorkArea.Left + 60;
        Top = SystemParameters.WorkArea.Top + 60;
    }

    private void RepositionInsideVirtualScreen()
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateLayout();
            const double margin = 14;
            var width = Math.Max(ActualWidth, 440);
            var height = Math.Max(ActualHeight, 120);
            var minLeft = SystemParameters.VirtualScreenLeft + margin;
            var minTop = SystemParameters.VirtualScreenTop + margin;
            var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - width - margin;
            var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - height - margin;
            Left = Math.Clamp(Left, minLeft, Math.Max(minLeft, maxLeft));
            Top = Math.Clamp(Top, minTop, Math.Max(minTop, maxTop));
        });
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
