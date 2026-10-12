using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LexVerse.Application.Modules;
using LexVerse.Application.Commerce;
using LexVerse.Application.Popup;
using LexVerse.Application.Product;
using LexVerse.Application.Privacy;
using LexVerse.Application.Realtime;
using LexVerse.App.Features.Popup;
using LexVerse.App.Features.Realtime;
using LexVerse.Core.Pipeline;
using LexVerse.Core.Product;
using LexVerse.Infrastructure.Windows;

namespace LexVerse.App;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4C56;
    private const int WmHotkey = 0x0312;
    private const uint VkF6 = 0x75;
    private const uint ModNoRepeat = 0x4000;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private readonly PopupFeatureModule _popupModule;
    private readonly RealtimeFeatureModule _realtimeModule;
    private readonly WindowsMonitorService _monitorService;
    private readonly IFeatureAccessService _featureAccessService;
    private readonly CommerceCoordinator _commerce;
    private readonly IPrivacyPreferencesService _privacyPreferences;
    private readonly int _moduleCount;
    private string _sourceLanguage = "auto";
    private string _targetLanguage = "vi";
    private string _hotkeyStatus = "F6 starting";
    private string _status = "Starting workspace...";
    private bool IsRealtimeRunning => _realtimeModule.Coordinator.State.Status is
        RealtimeCoordinatorStatus.Starting or RealtimeCoordinatorStatus.Running;
    private bool CanStartRegion => _regionAccessAllowed &&
        _privacyPreferences.Current.AllowRemoteTextProcessing && !IsRealtimeRunning && !_isSelectingRegion;
    private bool _regionAccessAllowed;
    private bool _fullScreenAccessAllowed;
    private CancellationTokenSource? _popupCancellation;
    private HwndSource? _hwndSource;
    private bool _hotkeyRegistered;
    private bool _isSelectingRegion;
    private bool _cleanupStarted;
    private bool _cleanupComplete;

    public MainWindow(
        PopupFeatureModule popupModule,
        RealtimeFeatureModule realtimeModule,
        FeatureModuleRegistry modules,
        WindowsMonitorService monitorService,
        IFeatureAccessService featureAccessService,
        CommerceCoordinator commerce,
        IPrivacyPreferencesService privacyPreferences)
    {
        ArgumentNullException.ThrowIfNull(popupModule);
        ArgumentNullException.ThrowIfNull(realtimeModule);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(monitorService);
        ArgumentNullException.ThrowIfNull(featureAccessService);
        ArgumentNullException.ThrowIfNull(commerce);
        ArgumentNullException.ThrowIfNull(privacyPreferences);

        _popupModule = popupModule;
        _realtimeModule = realtimeModule;
        _monitorService = monitorService;
        _featureAccessService = featureAccessService;
        _commerce = commerce;
        _privacyPreferences = privacyPreferences;
        InitializeComponent();
        InitializeHybridUiHost();
        _moduleCount = modules.Modules.Count;
        _realtimeModule.Coordinator.StateChanged += RealtimeCoordinator_StateChanged;
        _realtimeModule.Coordinator.FrameProcessed += RealtimeCoordinator_FrameProcessed;
        _commerce.StateChanged += Commerce_StateChanged;
        _privacyPreferences.Changed += PrivacyPreferences_Changed;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        _ = InitializeWebUiAsync();
        try
        {
            await _commerce.InitializeAsync();
        }
        catch
        {
            _status = "Account and plan information is temporarily unavailable.";
        }

        await RefreshFeatureAccessAsync();
    }

    private void PrivacyPreferences_Changed(object? sender, PrivacyPreferences preferences) =>
        Dispatcher.BeginInvoke(async () =>
        {
            await RefreshFeatureAccessAsync();
        });

    private void Commerce_StateChanged(object? sender, CommerceState state) =>
        Dispatcher.BeginInvoke(() => PublishDesktopSnapshot());

    private async Task RunCommerceOperationAsync(
        Func<CancellationToken, Task> operation,
        string status)
    {
        _status = status;
        try
        {
            await operation(CancellationToken.None);
            await RefreshFeatureAccessAsync();
            _status = "Account and feature access updated.";
        }
        catch (OperationCanceledException)
        {
            _status = "Account operation canceled.";
        }
        catch
        {
            _status = "Account operation failed. Check the network and try again.";
        }
    }

    private async Task RefreshFeatureAccessAsync()
    {
        var region = await _featureAccessService.GetAccessAsync(ProductFeatures.RegionTranslation);
        var fullScreen = await _featureAccessService.GetAccessAsync(ProductFeatures.FullScreenTranslation);
        _regionAccessAllowed = region.IsAllowed;
        _fullScreenAccessAllowed = fullScreen.IsAllowed;
        PublishDesktopSnapshot();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);
        _ = SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
        _hotkeyRegistered = RegisterHotKey(handle, HotkeyId, ModNoRepeat, VkF6);
        _hotkeyStatus = _hotkeyRegistered ? "F6 ready" : "F6 unavailable";
        _status = _hotkeyRegistered
            ? "Ready. Highlight text in another app and press F6."
            : "F6 is used by another app. Quick text remains available.";
    }

    private async Task TranslateQuickTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _status = "Enter text in Quick text first.";
            return;
        }

        await RunPopupAsync(
            token => _popupModule.UseCase.TranslateTextAsync(text, CreateRequest(), token),
            "Translating quick text…",
            translation => _lastQuickTranslation = translation);
    }

    private async Task StartRegionRealtimeAsync()
    {
        if (_isSelectingRegion)
        {
            return;
        }

        _isSelectingRegion = true;
        try
        {
            await _realtimeModule.Coordinator.StopAsync();
            WindowState = WindowState.Minimized;
            await Task.Delay(160);
            var selector = new SelectionOverlayWindow();
            var selected = selector.ShowDialog();
            WindowState = WindowState.Normal;
            Activate();

            if (selected != true || selector.Selection is not { } selection)
            {
                _status = "Region selection canceled.";
                return;
            }

            await StartRealtimeAsync(RealtimeCaptureKind.Region, selection);
        }
        catch (Exception exception)
        {
            ShowRealtimeStartError(exception);
        }
        finally
        {
            _isSelectingRegion = false;
            PublishDesktopSnapshot();
        }
    }

    private async Task StartFullScreenRealtimeAsync()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var monitorBounds = _monitorService.GetNearestMonitorBounds(handle);
            await StartRealtimeAsync(RealtimeCaptureKind.FullScreen, monitorBounds);
        }
        catch (Exception exception)
        {
            ShowRealtimeStartError(exception);
        }
    }

    private async Task StopRealtimeAsync()
    {
        try
        {
            await _realtimeModule.Coordinator.StopAsync();
            _status = "Realtime translation stopped.";
        }
        catch
        {
            _status = "Realtime cleanup did not finish normally.";
        }
    }

    private Task StartRealtimeAsync(RealtimeCaptureKind kind, LexVerse.Core.Geometry.ScreenRect bounds)
    {
        _status = kind == RealtimeCaptureKind.Region
            ? "Starting selected-region translation…"
            : "Starting full-screen translation…";
        var request = new RealtimeTranslationRequest(
            kind,
            bounds,
            GetOcrLanguageTag(),
            CreateRealtimeOptions());
        return _realtimeModule.Coordinator.StartAsync(request);
    }

    private RealtimeTranslationOptions CreateRealtimeOptions()
    {
        var sourceLanguage = _sourceLanguage;

        return RealtimeTranslationOptions.Document with
        {
            SourceLanguage = sourceLanguage,
            TargetLanguage = _targetLanguage
        };
    }

    private string GetOcrLanguageTag() => _sourceLanguage switch
    {
        "ja" => "ja-JP",
        "ko" => "ko-KR",
        "zh" => "zh-CN",
        "vi" => "vi-VN",
        _ => "en-US"
    };

    private void RealtimeCoordinator_StateChanged(object? sender, RealtimeCoordinatorState state)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (state.Status == RealtimeCoordinatorStatus.Faulted)
            {
                _status = "Realtime pipeline stopped after an error. Check provider and OCR availability.";
            }

            PublishDesktopSnapshot();
        });
    }

    private void RealtimeCoordinator_FrameProcessed(object? sender, RealtimeFrameUpdate update)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _status = update.UsedCachedTranslation
                ? $"Realtime: cached frame · {update.Timing.Total.TotalMilliseconds:0} ms"
                : $"Realtime: OCR {update.OcrBlockCount}, translated {update.TranslatedBlockCount} · {update.Timing.Total.TotalMilliseconds:0} ms";
            if (update.PerformanceBudgetExceeded)
            {
                _status += " · slower than target";
            }

            UpdateDesktopFrameTiming(update.Timing.Total.TotalMilliseconds, update.PerformanceBudgetExceeded);
            PublishDesktopSnapshot();
        });
    }

    private void ShowRealtimeStartError(Exception exception)
    {
        _status = exception is FeatureAccessDeniedException denied
            ? $"Realtime access unavailable: {denied.Decision.DenialReason}."
            : "Could not start realtime translation. Check capture permission, OCR language, and provider setup.";
    }

    private async Task RunSelectedTextPopupAsync()
    {
        await RunPopupAsync(
            token => _popupModule.UseCase.TranslateSelectedTextAsync(CreateRequest(), token),
            "Reading highlighted text…");
    }

    private async Task RunPopupAsync(
        Func<CancellationToken, Task<PopupTranslationResult>> operation,
        string loadingStatus,
        Action<string>? translationCompleted = null)
    {
        _popupCancellation?.Cancel();
        _popupCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _popupCancellation = cancellation;
        var cancellationToken = cancellation.Token;
        var popup = new TranslationPopupWindow();

        _status = loadingStatus;
        PublishDesktopSnapshot();
        try
        {
            var operationTask = operation(cancellationToken);
            var loadingDelay = Task.Delay(TimeSpan.FromMilliseconds(260), cancellationToken);
            if (await Task.WhenAny(operationTask, loadingDelay) != operationTask)
            {
                popup.Show();
            }

            var result = await operationTask;
            if (!result.HasSelection || result.Translation is null || result.SourceText is null)
            {
                popup.Close();
                _status = "No highlighted text found. Select text and try F6 again.";
                return;
            }

            if (!popup.IsVisible)
            {
                popup.Show();
            }

            popup.ShowResult(
                result.SourceText,
                result.Translation.TranslatedText,
                GetTargetLanguageLabel());
            translationCompleted?.Invoke(result.Translation.TranslatedText);
            _status = "Translation complete.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            popup.Close();
        }
        catch (RemoteProcessingConsentRequiredException)
        {
            popup.Close();
            _status = "Enable external text processing under Privacy before translating.";
        }
        catch (FeatureAccessDeniedException exception)
        {
            popup.Close();
            _status = $"Popup access unavailable: {exception.Decision.DenialReason}.";
        }
        catch
        {
            if (!popup.IsVisible)
            {
                popup.Show();
            }

            popup.ShowError(
                "Translation unavailable",
                "Check your provider credentials and network connection, then try again.");
            _status = "Translation provider unavailable.";
        }
        finally
        {
            if (ReferenceEquals(_popupCancellation, cancellation))
            {
                _popupCancellation = null;
            }

            cancellation.Dispose();
            PublishDesktopSnapshot();
        }
    }

    private PopupTranslationRequest CreateRequest()
    {
        var sourceLanguage = _sourceLanguage;
        return new PopupTranslationRequest(
            _targetLanguage,
            sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? null
                : sourceLanguage);
    }

    private string GetTargetLanguageLabel() => _targetLanguage switch
    {
        "vi" => "Vietnamese", "en" => "English", "ja" => "Japanese",
        "ko" => "Korean", "zh" => "Chinese", _ => "Translation"
    };

    private void CloseStartup_Click(object sender, RoutedEventArgs e) => Close();

    private IntPtr WndProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _ = RunSelectedTextPopupAsync();
        }

        return IntPtr.Zero;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_cleanupComplete)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_cleanupStarted)
        {
            return;
        }

        _cleanupStarted = true;
        _desktopUiLifetime?.Cancel();
        _startupTimer?.Stop();
        IsEnabled = false;
        _status = "Stopping background work…";
        try
        {
            await _realtimeModule.Coordinator.StopAsync();
        }
        catch
        {
        }

        _cleanupComplete = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, Close);
    }

    protected override void OnClosed(EventArgs e)
    {
        ShutdownHybridUiHost();
        _popupCancellation?.Cancel();
        _popupCancellation?.Dispose();
        _popupCancellation = null;

        var handle = new WindowInteropHelper(this).Handle;
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(handle, HotkeyId);
        }

        _hwndSource?.RemoveHook(WndProc);
        _realtimeModule.Coordinator.StateChanged -= RealtimeCoordinator_StateChanged;
        _realtimeModule.Coordinator.FrameProcessed -= RealtimeCoordinator_FrameProcessed;
        _commerce.StateChanged -= Commerce_StateChanged;
        _privacyPreferences.Changed -= PrivacyPreferences_Changed;
        base.OnClosed(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
}
