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
        private ScreenRect? _appliedPhysicalBounds;
        private double? _rotationAngle;
        private (ScreenRect Bounds, IntPtr Target, IntPtr InsertAfter)? _layerState;
        private List<Occlusion> _occlusions = [];
        public bool IsExcludedFromCapture { get; private set; }
        public IntPtr TargetWindow { get; set; }
        private IntPtr _priorityWindow;
        public IntPtr PriorityWindow
        {
            get => _priorityWindow;
            set
            {
                if (_priorityWindow == value) return;
                _priorityWindow = value;
                if (!IsLoaded || TargetWindow != IntPtr.Zero) return;
                _ = SetWindowPos(new WindowInteropHelper(this).Handle,
                    GetBoundsInsertAfter(TargetWindow, value), 0, 0, 0, 0,
                    0x0001 | 0x0002 | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
            }
        }

        internal static IntPtr GetBoundsInsertAfter(IntPtr targetWindow, IntPtr priorityWindow) =>
            targetWindow != IntPtr.Zero ? new IntPtr(-2) :
            priorityWindow != IntPtr.Zero ? priorityWindow : HWND_TOPMOST;

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
            IsExcludedFromCapture = SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
            if (!IsExcludedFromCapture)
                System.Diagnostics.Trace.TraceWarning("Overlay capture exclusion failed: {0}", Marshal.GetLastWin32Error());
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

            if (IsLoaded && _appliedPhysicalBounds == physicalBounds &&
                _boundsDpiScaleX == dpiScaleX && _boundsDpiScaleY == dpiScaleY) return;

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
            _appliedPhysicalBounds = SetWindowPosToPhysicalBounds(physicalBounds) ? physicalBounds : null;
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
            if (_rotationAngle == normalizedAngle) return;
            _rotationAngle = normalizedAngle;
            OverlayItemsControl.RenderTransform = normalizedAngle == 0
                ? Transform.Identity
                : new RotateTransform(normalizedAngle);
        }

        private bool SetWindowPosToPhysicalBounds(ScreenRect physicalBounds)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            return SetWindowPos(
                hwnd,
                GetBoundsInsertAfter(TargetWindow, PriorityWindow),
                (int)Math.Round(physicalBounds.X),
                (int)Math.Round(physicalBounds.Y),
                (int)Math.Round(physicalBounds.Width),
                (int)Math.Round(physicalBounds.Height),
                SWP_NOACTIVATE | SWP_NOOWNERZORDER | (TargetWindow == IntPtr.Zero ? 0u : 0x0004u));
        }

        /// <summary>Keep the overlay immediately above its target, with occluded pixels removed.</summary>
        public void UpdateWindowLayer(ScreenRect bounds)
        {
            if (TargetWindow == IntPtr.Zero || !IsLoaded) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            var previous = GetWindow(TargetWindow, 3); // GW_HWNDPREV
            var insertAfter = IntPtr.Zero;
            var occlusions = new List<Occlusion>();
            for (var other = previous; other != IntPtr.Zero; other = GetWindow(other, 3))
            {
                if (other == hwnd) continue;
                if (insertAfter == IntPtr.Zero) insertAfter = other;
                if (!IsWindowVisible(other) || IsIconic(other)) continue;
                if (DwmGetWindowAttribute(other, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) continue;
                if (!GetWindowRect(other, out var rect)) continue;
                if (rect.Right <= bounds.X || rect.Left >= bounds.Right ||
                    rect.Bottom <= bounds.Y || rect.Top >= bounds.Bottom) continue;
                occlusions.Add(new Occlusion(other, rect.Left, rect.Top, rect.Right, rect.Bottom));
            }
            var state = (bounds, TargetWindow, insertAfter);
            if (_layerState == state && _occlusions.SequenceEqual(occlusions) &&
                GetWindow(hwnd, 3) == insertAfter) return;
            var region = CreateRectRgn(0, 0, (int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
            if (region == IntPtr.Zero) return;
            foreach (var rect in occlusions)
            {
                var cut = CreateRectRgn(rect.Left - (int)bounds.X, rect.Top - (int)bounds.Y,
                    rect.Right - (int)bounds.X, rect.Bottom - (int)bounds.Y);
                if (cut == IntPtr.Zero) { _ = DeleteObject(region); return; }
                var combined = CombineRgn(region, region, cut, 4); // RGN_DIFF
                _ = DeleteObject(cut);
                if (combined == 0) { _ = DeleteObject(region); return; }
            }
            if (SetWindowRgn(hwnd, region, true) == 0) { _ = DeleteObject(region); return; }
            if (!SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, 0x0001 | 0x0002 | SWP_NOACTIVATE)) return;
            _layerState = state;
            _occlusions = occlusions;
        }

        private readonly record struct Occlusion(IntPtr Handle, int Left, int Top, int Right, int Bottom);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("gdi32.dll")] private static extern int CombineRgn(IntPtr destination, IntPtr first, IntPtr second, int mode);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

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
