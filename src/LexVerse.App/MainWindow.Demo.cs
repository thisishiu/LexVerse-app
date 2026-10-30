using System.Windows;
using LexVerse.App.Demo;
using LexVerse.Application.Realtime;
using LexVerse.Core.Capture;
using LexVerse.Core.Geometry;

namespace LexVerse.App;

public partial class MainWindow
{
    private DemoSession? _demo;

    internal void AttachDemo(DemoSession demo)
    {
        _demo = demo;
        demo.Changed += (_, _) => PublishDesktopSnapshot();
        Loaded += async (_, _) => await RunDemoOperationAsync(demo.OpenAsync);
    }

    private object DemoStyle() => new
    {
        opacity = _desktopPreferences.Current.OverlayOpacityPercent / 100.0,
        fontScale = _desktopPreferences.Current.OverlayFontScalePercent / 100.0,
        dark = _desktopPreferences.Current.UseDarkOverlayBlocks,
        closeOnOutsideClick = _desktopPreferences.Current.ClosePopupOnOutsideClick,
        allowDragging = _desktopPreferences.Current.AllowPopupDragging
    };

    private async Task RunDemoOperationAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception e) when (!_cleanupStarted)
        {
            _status = e.Message;
            MessageBox.Show(this, e.Message, "LexVerse", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { PublishDesktopSnapshot(); }
    }

    private async Task StartDemoRealtimeAsync(RealtimeCaptureKind kind, ScreenRect bounds, WindowCaptureTarget? window = null)
    {
        if (!_privacyPreferences.Current.AllowRemoteTextProcessing)
            throw new InvalidOperationException("Hãy bật xử lý văn bản trong cài đặt trước khi bắt đầu.");
        _realtimeStartErrorCode = null;
        await _demo!.StartAsync(kind, bounds, _sourceLanguage, _targetLanguage, DemoStyle(), window);
        _status = "Translation running.";
        PublishDesktopSnapshot();
    }

    private Task RunDemoPopupAsync() => RunDemoOperationAsync(async () =>
    {
        if (!_privacyPreferences.Current.AllowRemoteTextProcessing)
            throw new InvalidOperationException("Hãy bật xử lý văn bản trong cài đặt trước khi bắt đầu.");
        var result = await _demo!.PopupAsync(_sourceLanguage, _targetLanguage, DemoStyle());
        _status = string.IsNullOrWhiteSpace(result) ? "Select text and try the shortcut again." : "Translation complete.";
    });
}
