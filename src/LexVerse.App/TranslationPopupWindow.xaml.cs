using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;
using System.IO;
using System.Text.Json;
using LexVerse.Application.Popup;
using Microsoft.Web.WebView2.Core;

namespace LexVerse.App;

public partial class TranslationPopupWindow : Window
{
    private const int VkLeftButton = 0x01;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private readonly bool _closeOnOutsideClick;
    private readonly bool _allowDragging;
    private readonly DispatcherTimer _outsideClickTimer;
    private bool _wasLeftButtonPressed;
    private bool _closed;
    private bool _rendererReady;
    private readonly bool _vietnamese;
    private string _markdown = "";
    private object? _pendingResult;
    private Task? _rendererTask;
    private const string PopupHost = "popup.lexverse.local";

    public TranslationPopupWindow(
        bool closeOnOutsideClick = true,
        bool allowDragging = true,
        bool vietnamese = false)
    {
        InitializeComponent();
        _closeOnOutsideClick = closeOnOutsideClick;
        _allowDragging = allowDragging;
        _vietnamese = vietnamese;
        CopyButton.Content = vietnamese ? "Sao chép" : "Copy";
        LoadingText.Text = vietnamese ? "Đang dịch…" : "Translating…";
        CloseButton.ToolTip = vietnamese ? "Đóng" : "Close";
        System.Windows.Automation.AutomationProperties.SetName(CloseButton, vietnamese ? "Đóng" : "Close");
        _outsideClickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _outsideClickTimer.Tick += OutsideClickTimer_Tick;
        PositionNearCursor();
    }

    public void ShowResult(string translatedText, IReadOnlyList<PopupTerm>? terms = null)
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        ResultView.Visibility = Visibility.Visible;
        _markdown = translatedText;
        _pendingResult = new { markdown = translatedText, terms = (terms ?? []).Select(term => new
            { term = term.Term, translation = term.Translation, explanation = term.Explanation }),
            termsLabel = _vietnamese ? "Thuật ngữ" : "Terminology" };
        _rendererTask ??= InitializeRendererAsync();
        SendResult();
        RepositionInsideVirtualScreen();
    }

    public void ShowError(string title, string message)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultView.Visibility = Visibility.Collapsed;
        CopyButton.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        ErrorTitle.Text = title;
        ErrorMessage.Text = message;
        Height = Math.Min(240, MaxHeight);
        RepositionInsideVirtualScreen();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_markdown);
            CopyButton.Content = _vietnamese ? "Đã chép" : "Copied";
        }
        catch
        {
            CopyButton.Content = _vietnamese ? "Không chép được" : "Copy failed";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_allowDragging && e.ChangedButton == MouseButton.Left)
        {
            DragMove();
            RepositionInsideVirtualScreen();
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
        _closed = true;
        ResultView.Dispose();
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
        // Temporarily disabled for demo recording; restore capture exclusion afterward.
        // _ = SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, WdaExcludeFromCapture);
        if (GetCursorPos(out var cursor))
            SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, cursor.X + 18, cursor.Y + 18, 0, 0, 0x0015);
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
            if (_closed) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info)) return;
            var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
            MaxHeight = (info.Work.Bottom - info.Work.Top) / scale.M22 * .8;
            MaxWidth = Math.Max(1, (info.Work.Right - info.Work.Left - 28) / scale.M11);
            Width = Math.Min(520, MaxWidth);
            if (Height > MaxHeight) Height = MaxHeight;
            UpdateLayout();
            if (!GetWindowRect(hwnd, out var rect)) return;
            var x = Math.Clamp(rect.Left, info.Work.Left + 14, Math.Max(info.Work.Left + 14, info.Work.Right - (rect.Right - rect.Left) - 14));
            var y = Math.Clamp(rect.Top, info.Work.Top + 14, Math.Max(info.Work.Top + 14, info.Work.Bottom - (rect.Bottom - rect.Top) - 14));
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, 0x0015);
        });
    }

    private async Task InitializeRendererAsync()
    {
        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "DesktopUi");
            if (!File.Exists(Path.Combine(assets, "popup.html"))) throw new FileNotFoundException();
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LexVerse", "PopupWebView2"));
            if (_closed) return;
            await ResultView.EnsureCoreWebView2Async(environment);
            if (_closed) return;
            var core = ResultView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.SetVirtualHostNameToFolderMapping(PopupHost, assets, CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess) ShowRendererError();
            };
            core.ProcessFailed += (_, _) => ShowRendererError();
            core.NavigationStarting += (_, args) => args.Cancel = args.Uri != $"https://{PopupHost}/popup.html";
            core.FrameNavigationStarting += (_, args) => args.Cancel = true;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.DownloadStarting += (_, args) => args.Cancel = true;
            core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != PopupHost || !uri.IsDefaultPort)
                    args.Response = environment.CreateWebResourceResponse(null, 403, "Blocked", "");
            };
            core.WebMessageReceived += (_, args) =>
            {
                if (_closed || args.Source != $"https://{PopupHost}/popup.html") return;
                try
                {
                    using var message = JsonDocument.Parse(args.WebMessageAsJson);
                    var root = message.RootElement;
                    if (root.GetProperty("type").GetString() == "ready") { _rendererReady = true; SendResult(); }
                    else if (root.GetProperty("type").GetString() == "rendered" && _pendingResult is not null)
                    {
                        LoadingPanel.Visibility = Visibility.Collapsed;
                        CopyButton.Visibility = Visibility.Visible;
                    }
                    else if (root.GetProperty("type").GetString() == "height" && _pendingResult is not null && root.GetProperty("value").TryGetDouble(out var height) && double.IsFinite(height))
                    {
                        Height = Math.Clamp(height + 50, Math.Min(140, MaxHeight), MaxHeight);
                        RepositionInsideVirtualScreen();
                    }
                }
                catch (JsonException) { }
            };
            core.Navigate($"https://{PopupHost}/popup.html");
        }
        catch (Exception)
        {
            ShowRendererError();
        }
    }

    private void ShowRendererError()
    {
        if (_closed) return;
        _rendererReady = false;
        _pendingResult = null;
        ShowError(_vietnamese ? "Không hiển thị được bản dịch" : "Cannot display translation",
            _vietnamese ? "Không tải được giao diện popup. Bạn vẫn có thể sao chép bản dịch." : "The popup renderer could not load. You can still copy the translation.");
        CopyButton.Visibility = Visibility.Visible;
    }

    private void SendResult()
    {
        if (_rendererReady && !_closed && _pendingResult is not null)
            ResultView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(_pendingResult));
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

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
