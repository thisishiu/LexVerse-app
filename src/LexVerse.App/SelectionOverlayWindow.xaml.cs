using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LexVerse.App;

public partial class SelectionOverlayWindow : Window
{
    private const int VkEscape = 0x1B;
    private Point? _startPoint;
    private readonly DispatcherTimer _escapeTimer;

    public LexVerse.Core.Models.ScreenSelection? Selection { get; private set; }

    public SelectionOverlayWindow()
    {
        InitializeComponent();
        _escapeTimer = CreateEscapeTimer();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) =>
        {
            Activate();
            Focus();
            Keyboard.Focus(this);
            _escapeTimer.Start();
        };
        Closed += (_, _) => _escapeTimer.Stop();
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(SelectionCanvas);
        SelectionBorder.Visibility = Visibility.Visible;
        Hint.Visibility = Visibility.Collapsed;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_startPoint is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(SelectionCanvas);
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();

        if (_startPoint is not { } start)
        {
            DialogResult = false;
            return;
        }

        var end = e.GetPosition(SelectionCanvas);
        var x = (int)(Math.Min(start.X, end.X) + Left);
        var y = (int)(Math.Min(start.Y, end.Y) + Top);
        var width = (int)Math.Abs(end.X - start.X);
        var height = (int)Math.Abs(end.Y - start.Y);

        Selection = new LexVerse.Core.Models.ScreenSelection(x, y, width, height);
        DialogResult = Selection.IsMeaningful;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelSelection();
        }
    }

    private DispatcherTimer CreateEscapeTimer()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };

        timer.Tick += (_, _) =>
        {
            if ((GetAsyncKeyState(VkEscape) & 0x8000) != 0)
            {
                CancelSelection();
            }
        };

        return timer;
    }

    private void CancelSelection()
    {
        ReleaseMouseCapture();
        Selection = null;
        DialogResult = false;
    }
}
