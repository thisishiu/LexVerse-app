using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LexVerse.Application.Desktop;
using LexVerse.Application.Realtime;
using LexVerse.Core.Overlay;
using LexVerse.Overlay;
using LexVerse.Overlay.Models;
using LexVerse.Infrastructure.Windows;
using OverlayWindow = LexVerse.Overlay.MainWindow;

namespace LexVerse.App.Features.Realtime;

public sealed class WpfRealtimeOverlayOutput : IRealtimeOutput, IDisposable
{
    private const double HorizontalPadding = 4;
    private const double VerticalPadding = 2;
    private const double MinFontSize = 8;
    private readonly ObservableCollection<TextItem> _items = [];
    private readonly OverlayItemCollection _itemCollection;
    private readonly OverlayTextLayoutCache _layoutCache = new();
    private long _generation;
    private Guid _sessionId;
    private long _frameSequence;
    private bool _lastIsPartial;
    private readonly Dispatcher _dispatcher;
    private readonly IDesktopPreferencesService _preferences;
    private OverlayWindow? _window;
    private OverlayRenderFrame? _lastFrame;
    private bool _disposed;
    private readonly DispatcherTimer _windowTimer;
    private readonly Win32WindowTracker _tracker = new();
    private Window? _mainWindow;
    private bool? _originalMainWindowTopmost;

    internal void AttachMainWindow(Window window)
    {
        _dispatcher.VerifyAccess();
        DetachMainWindow();
        _mainWindow = window;
        window.Activated += MainWindowLayerChanged;
        window.Deactivated += MainWindowLayerChanged;
        window.StateChanged += MainWindowLayerChanged;
        window.IsVisibleChanged += WindowVisibilityChanged;
        window.Closed += MainWindowClosed;
        SynchronizeMainWindowLayer();
    }

    private void MainWindowLayerChanged(object? sender, EventArgs e) => SynchronizeMainWindowLayer();

    private void WindowVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        SynchronizeMainWindowLayer();

    private void MainWindowClosed(object? sender, EventArgs e) => DetachMainWindow();

    private void RestoreMainWindowLayer()
    {
        if (_mainWindow is not null && _originalMainWindowTopmost is { } original)
        {
            _originalMainWindowTopmost = null;
            if (_mainWindow.Topmost != original)
                _mainWindow.SetCurrentValue(Window.TopmostProperty, original);
        }
    }

    private void DetachMainWindow()
    {
        if (_window is not null) _window.PriorityWindow = IntPtr.Zero;
        RestoreMainWindowLayer();
        if (_mainWindow is not { } window) return;
        window.Activated -= MainWindowLayerChanged;
        window.Deactivated -= MainWindowLayerChanged;
        window.StateChanged -= MainWindowLayerChanged;
        window.IsVisibleChanged -= WindowVisibilityChanged;
        window.Closed -= MainWindowClosed;
        _mainWindow = null;
    }

    private void SynchronizeMainWindowLayer(bool preparingToShow = false)
    {
        if (_mainWindow is not { IsActive: true, IsVisible: true } main ||
            main.WindowState == WindowState.Minimized || _window is null ||
            (!_window.IsVisible && !preparingToShow))
        {
            if (_window is not null) _window.PriorityWindow = IntPtr.Zero;
            RestoreMainWindowLayer();
            return;
        }

        _originalMainWindowTopmost ??= main.Topmost;
        if (!main.Topmost) main.SetCurrentValue(Window.TopmostProperty, true);
        _window.PriorityWindow = new WindowInteropHelper(main).Handle;
    }

    public WpfRealtimeOverlayOutput(
        Dispatcher dispatcher,
        IDesktopPreferencesService preferences)
    {
        _itemCollection = new OverlayItemCollection(_items);
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _preferences.Changed += Preferences_Changed;
        _windowTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background,
            (_, _) => UpdateWindowPlacement(), dispatcher);
        _windowTimer.Stop();
    }

    public Task RenderAsync(
        RealtimeFrameUpdate update,
        bool isPartial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var generation = Interlocked.Read(ref _generation);
        return RunOnUiAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || generation != Interlocked.Read(ref _generation)) return;
            if (_sessionId == update.SessionId && update.FrameSequence < _frameSequence) return;
            _sessionId = update.SessionId;
            _frameSequence = update.FrameSequence;
            RenderOnUi(update.OverlayFrame, isPartial);
        }, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _generation);
        return RunOnUiAsync(ClearOnUi, cancellationToken);
    }

    private void RenderOnUi(OverlayRenderFrame frame, bool isPartial = false)
    {
        _lastFrame = frame;
        _lastIsPartial = isPartial;
        if (frame.Trace is null)
        {
            InvalidateWindowFrame();
            return;
        }

        if (_window is null)
        {
            _window = new OverlayWindow(_items);
            _window.IsVisibleChanged += WindowVisibilityChanged;
        }
        _window.TargetWindow = frame.WindowTarget?.Hwnd ?? IntPtr.Zero;
        SynchronizeMainWindowLayer(preparingToShow: true);
        var topmost = frame.WindowTarget is null;
        if (_window.Topmost != topmost) _window.Topmost = topmost;
        if (frame.WindowTarget is not null)
        {
            _windowTimer.Start();
            if (!IsFrameCurrent(frame)) { InvalidateWindowFrame(); return; }
        }
        else _windowTimer.Stop();
        _window.SetPhysicalBounds(
            frame.Trace.Geometry.SourceScreenRect,
            frame.Trace.Geometry.DpiScaleX,
            frame.Trace.Geometry.DpiScaleY);
        _window.SetContentRotation(frame.RotationAngle);
        _window.Opacity = _preferences.Current.OverlayOpacityPercent / 100d;
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _itemCollection.Update(frame, isPartial, new OverlayRenderSettings(
            VisualTreeHelper.GetDpi(_window).PixelsPerDip, _preferences.Current.OverlayFontScalePercent,
            _preferences.Current.UseDarkOverlayBlocks, _preferences.Current.ShowOcrDebugBoxes),
            CreateTextItem, CreateDebugItem);
        UpdateWindowPlacement();
    }

    private bool IsFrameCurrent(OverlayRenderFrame frame)
    {
        if (frame.WindowTarget is not { } target || frame.Trace is null) return false;
        if (!WindowTargetRegistry.IsCurrent(target)) return false;
        var snapshot = _tracker.GetSnapshot(target.Hwnd);
        return snapshot.IsVisible && !snapshot.IsMinimized &&
            snapshot.WindowRect == frame.Trace.Geometry.SourceScreenRect &&
            snapshot.Dpi.ScaleX == frame.Trace.Geometry.DpiScaleX &&
            snapshot.Dpi.ScaleY == frame.Trace.Geometry.DpiScaleY;
    }

    private void UpdateWindowPlacement()
    {
        if (_window is null || _lastFrame?.WindowTarget is null || _lastFrame.Trace is null) return;
        var target = _lastFrame.WindowTarget;
        if (!WindowTargetRegistry.IsCurrent(target)) { InvalidateWindowFrame(); return; }
        var snapshot = _tracker.GetSnapshot(target.Hwnd);
        var geometry = _lastFrame.Trace!.Geometry;
        if (!snapshot.IsVisible || snapshot.IsMinimized ||
            snapshot.WindowRect.Width != geometry.SourceScreenRect.Width ||
            snapshot.WindowRect.Height != geometry.SourceScreenRect.Height ||
            snapshot.Dpi.ScaleX != geometry.DpiScaleX || snapshot.Dpi.ScaleY != geometry.DpiScaleY)
        { InvalidateWindowFrame(); return; }
        _window.SetPhysicalBounds(snapshot.WindowRect, snapshot.Dpi.ScaleX, snapshot.Dpi.ScaleY);
        if (!_window.IsVisible) _window.Show();
        _window.UpdateWindowLayer(snapshot.WindowRect);
    }

    private void InvalidateWindowFrame()
    {
        _windowTimer.Stop();
        _window?.Hide();
        _lastFrame = null;
        _sessionId = Guid.Empty;
        _frameSequence = 0;
        _itemCollection.Clear();
    }

    private TextItem CreateTextItem(OverlayTextItem item)
    {
        var window = _window ?? throw new InvalidOperationException("Overlay window is not initialized.");
        var targetRect = OverlayTextLayoutCalculator.ClipToBounds(
            window.ScreenPhysicalToLocalDip(OverlaySourceGeometry.GetMaskBounds(item.TargetRect)),
            window.Width,
            window.Height);
        var x = targetRect.X;
        var y = targetRect.Y;
        var maxWidth = Math.Max(0, window.Width - x);
        var fontRect = window.ScreenPhysicalToLocalDip(
            new(0, 0, 1, Math.Max(1, item.FontSize)));
        var fontScale = _preferences.Current.OverlayFontScalePercent / 100d;
        var layout = _layoutCache.Calculate(
            item.TranslatedText,
            targetRect.Width,
            targetRect.Height,
            maxWidth,
            Math.Max(MinFontSize, OverlaySourceGeometry.FontSizeFromInkHeight(fontRect.Height) * fontScale),
            MinFontSize,
            HorizontalPadding,
            VerticalPadding,
            VisualTreeHelper.GetDpi(window).PixelsPerDip);

        var useDarkBlocks = _preferences.Current.UseDarkOverlayBlocks;
        return new TextItem
        {
            Text = item.TranslatedText,
            X = x,
            Y = y,
            Width = layout.Width,
            Height = layout.Height,
            MinHeight = layout.Height,
            FontSize = layout.FontSize,
            Background = useDarkBlocks ? "#F018181B" : "#F5FFFFFF",

            // khung viền của box
            // BorderBrush = useDarkBlocks ? "#4DFFFFFF" : "#29000000",
            BorderBrush = "Transparent",
            
            Foreground = useDarkBlocks ? "White" : "#111827"
        };
    }

    private TextItem CreateDebugItem(OverlayTextItem item)
    {
        var window = _window ?? throw new InvalidOperationException("Overlay window is not initialized.");
        var rect = OverlayTextLayoutCalculator.ClipToBounds(
            window.ScreenPhysicalToLocalDip(item.TargetRect),
            window.Width,
            window.Height);
        return new TextItem
        {
            Text = string.Empty,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            MinHeight = rect.Height,
            FontSize = MinFontSize,
            Background = "Transparent",
            BorderBrush = "#FF7A00",
            Foreground = "Transparent"
        };
    }

    private void ClearOnUi()
    {
        _windowTimer.Stop();
        _lastFrame = null;
        _sessionId = Guid.Empty;
        _frameSequence = 0;
        if (_window is not null)
        {
            _window.IsVisibleChanged -= WindowVisibilityChanged;
            _window.Close();
        }
        _window = null;
        RestoreMainWindowLayer();
        _itemCollection.Clear();
        _layoutCache.Clear();
    }

    private Task RunOnUiAsync(Action action, CancellationToken cancellationToken)
    {
        if (_dispatcher.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(
            action,
            DispatcherPriority.Render,
            cancellationToken).Task;
    }

    private void Preferences_Changed(object? sender, DesktopPreferences preferences)
    {
        if (_disposed || _lastFrame is null)
        {
            return;
        }

        _ = _dispatcher.BeginInvoke(
            () =>
            {
                if (!_disposed && _lastFrame is { } frame)
                {
                    RenderOnUi(frame, _lastIsPartial);
                }
            },
            DispatcherPriority.Render);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _preferences.Changed -= Preferences_Changed;
        if (_dispatcher.CheckAccess())
        {
            ClearOnUi();
            DetachMainWindow();
        }
        else if (!_dispatcher.HasShutdownStarted)
        {
            _dispatcher.Invoke(() =>
            {
                ClearOnUi();
                DetachMainWindow();
            });
        }
    }
}
