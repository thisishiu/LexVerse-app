using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using LexVerse.Core.Geometry;
using LexVerse.Overlay.Models;

namespace LexVerse.Overlay
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<TextItem> Items { get; }

        public ScreenPoint VirtualScreenOrigin { get; private set; }

        private ScreenRect? _pendingPhysicalBounds;
        private double _boundsDpiScaleX = 1;
        private double _boundsDpiScaleY = 1;

        public MainWindow()
            : this(new ObservableCollection<TextItem>())
        {
        }

        public MainWindow(ObservableCollection<TextItem> items)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            InitializeComponent();
            DataContext = this;
        }

        private void TranslationBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border box)
            {
                var (padding, border) = OverlayTextLayoutCalculator.GetInsets(e.NewSize.Width, e.NewSize.Height);
                box.Padding = padding;
                box.BorderThickness = border;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_pendingPhysicalBounds is { } pendingPhysicalBounds)
            {
                SetPhysicalBounds(pendingPhysicalBounds, _boundsDpiScaleX, _boundsDpiScaleY);
            }
            else
            {
                var virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
                var virtualTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
                var virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                var virtualHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

                SetPhysicalBounds(new ScreenRect(virtualLeft, virtualTop, virtualWidth, virtualHeight));
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            _ = SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        }

        public void SetPhysicalBounds(ScreenRect physicalBounds)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            SetPhysicalBounds(physicalBounds, dpi.DpiScaleX, dpi.DpiScaleY);
        }

        public void SetPhysicalBounds(ScreenRect physicalBounds, double dpiScaleX, double dpiScaleY)
        {
            physicalBounds.Validate();

            if (dpiScaleX <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dpiScaleX), "DPI scale must be greater than zero.");
            }

            if (dpiScaleY <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dpiScaleY), "DPI scale must be greater than zero.");
            }

            _boundsDpiScaleX = dpiScaleX;
            _boundsDpiScaleY = dpiScaleY;

            if (!IsLoaded)
            {
                _pendingPhysicalBounds = physicalBounds;
                return;
            }

            VirtualScreenOrigin = new ScreenPoint(physicalBounds.X, physicalBounds.Y);
            Left = physicalBounds.X / dpiScaleX;
            Top = physicalBounds.Y / dpiScaleY;
            Width = physicalBounds.Width / dpiScaleX;
            Height = physicalBounds.Height / dpiScaleY;
            SetWindowPosToPhysicalBounds(physicalBounds);
            _pendingPhysicalBounds = null;
        }

        public Rect ScreenPhysicalToLocalDip(ScreenRect physicalRect)
        {
            return new PhysicalPixelToDipConverter(
                VirtualScreenOrigin,
                _boundsDpiScaleX,
                _boundsDpiScaleY).ToLocalDip(physicalRect);
        }

        public void SetContentRotation(double clockwiseAngle)
        {
            var normalizedAngle = double.IsFinite(clockwiseAngle) && Math.Abs(clockwiseAngle) >= 0.01
                ? clockwiseAngle
                : 0;
            OverlayItemsControl.RenderTransform = normalizedAngle == 0
                ? Transform.Identity
                : new RotateTransform(normalizedAngle);
        }

        private void SetWindowPosToPhysicalBounds(ScreenRect physicalBounds)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            _ = SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                (int)Math.Round(physicalBounds.X),
                (int)Math.Round(physicalBounds.Y),
                (int)Math.Round(physicalBounds.Width),
                (int)Math.Round(physicalBounds.Height),
                SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
    }
}
