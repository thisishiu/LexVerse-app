using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using LexVerse.Application.Desktop;
using LexVerse.Application.Realtime;
using LexVerse.Core.Overlay;
using LexVerse.Overlay;
using LexVerse.Overlay.Models;
using OverlayWindow = LexVerse.Overlay.MainWindow;

namespace LexVerse.App.Features.Realtime;

public sealed class WpfRealtimeOverlayOutput : IRealtimeOutput, IDisposable
{
    private const double HorizontalPadding = 4;
    private const double VerticalPadding = 2;
    private const double MinFontSize = 8;
    private const double MaxFontSize = 44;
    private readonly ObservableCollection<TextItem> _items = [];
    private readonly Dispatcher _dispatcher;
    private readonly IDesktopPreferencesService _preferences;
    private OverlayWindow? _window;
    private OverlayRenderFrame? _lastFrame;
    private bool _disposed;

    public WpfRealtimeOverlayOutput(
        Dispatcher dispatcher,
        IDesktopPreferencesService preferences)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _preferences.Changed += Preferences_Changed;
    }

    public Task RenderAsync(
        RealtimeFrameUpdate update,
        bool isPartial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        return RunOnUiAsync(() => RenderOnUi(update.OverlayFrame), cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        RunOnUiAsync(ClearOnUi, cancellationToken);

    private void RenderOnUi(OverlayRenderFrame frame)
    {
        _lastFrame = frame;
        if (frame.Trace is null)
        {
            _window?.Hide();
            return;
        }

        _window ??= new OverlayWindow(_items);
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

        var nextItems = frame.Items.Select(CreateTextItem)
            .Where(item => item.Width > 0 && item.Height > 0).ToList();
        if (_preferences.Current.ShowOcrDebugBoxes)
        {
            nextItems.AddRange(frame.Items.Select(CreateDebugItem)
                .Where(item => item.Width > 0 && item.Height > 0));
        }

        ReconcileItems(nextItems);
    }

    private TextItem CreateTextItem(OverlayTextItem item)
    {
        var window = _window ?? throw new InvalidOperationException("Overlay window is not initialized.");
        var targetRect = OverlayTextLayoutCalculator.ClipToBounds(
            window.ScreenPhysicalToLocalDip(item.TargetRect),
            window.Width,
            window.Height);
        var x = targetRect.X;
        var y = targetRect.Y;
        var maxWidth = Math.Max(0, window.Width - x);
        var fontRect = window.ScreenPhysicalToLocalDip(
            new(0, 0, 1, Math.Max(1, item.FontSize)));
        var fontScale = _preferences.Current.OverlayFontScalePercent / 100d;
        var layout = OverlayTextLayoutCalculator.Calculate(
            item.TranslatedText,
            targetRect.Width,
            targetRect.Height,
            maxWidth,
            Math.Clamp(fontRect.Height * fontScale, MinFontSize, MaxFontSize),
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

    private void ReconcileItems(IReadOnlyList<TextItem> nextItems)
    {
        for (var index = 0; index < nextItems.Count; index++)
        {
            if (index >= _items.Count)
            {
                _items.Add(nextItems[index]);
                continue;
            }

            Copy(nextItems[index], _items[index]);
        }

        while (_items.Count > nextItems.Count)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    private static void Copy(TextItem source, TextItem target)
    {
        target.Text = source.Text;
        target.X = source.X;
        target.Y = source.Y;
        target.Width = source.Width;
        target.Height = source.Height;
        target.MinHeight = source.MinHeight;
        target.FontSize = source.FontSize;
        target.Background = source.Background;
        target.BorderBrush = source.BorderBrush;
        target.Foreground = source.Foreground;
    }

    private void ClearOnUi()
    {
        _lastFrame = null;
        _window?.Close();
        _window = null;
        _items.Clear();
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
                    RenderOnUi(frame);
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
        }
        else if (!_dispatcher.HasShutdownStarted)
        {
            _dispatcher.Invoke(ClearOnUi);
        }
    }
}
