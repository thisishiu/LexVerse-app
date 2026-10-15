using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using LexVerse.Application.Desktop;
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
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Windows;
using LexVerse.Infrastructure.Diagnostics;

namespace LexVerse.App;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4C56;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWindows = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private readonly PopupFeatureModule _popupModule;
    private readonly RealtimeFeatureModule _realtimeModule;
    private readonly WindowsMonitorService _monitorService;
    private readonly IFeatureAccessService _featureAccessService;
    private readonly CommerceCoordinator _commerce;
    private readonly IPrivacyPreferencesService _privacyPreferences;
    private readonly IDesktopPreferencesService _desktopPreferences;
    private readonly WpfRealtimeOverlayOutput _realtimeOutput;
    private readonly int _moduleCount;
    private string _sourceLanguage = "auto";
    private string _targetLanguage = "vi";
    private string _hotkeyStatus = "F6 starting";
    private string _status = "Starting workspace...";
    private bool IsRealtimeRunning => _realtimeModule.Coordinator.State.Status is
        RealtimeCoordinatorStatus.Starting or RealtimeCoordinatorStatus.Running;
    private bool CanStartRegion => _regionAccessAllowed &&
        _privacyPreferences.Current.AllowRemoteTextProcessing && !IsRealtimeRunning && !_isSelectingRegion;
    private bool CanStartFullScreen => _fullScreenAccessAllowed &&
        _privacyPreferences.Current.AllowRemoteTextProcessing && !IsRealtimeRunning;
    private bool _regionAccessAllowed;
    private bool _fullScreenAccessAllowed;
    private CancellationTokenSource? _popupCancellation;
    private HwndSource? _hwndSource;
    private bool _hotkeyRegistered;
    private string _registeredPopupShortcut = "F6";
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
        IPrivacyPreferencesService privacyPreferences,
        IDesktopPreferencesService desktopPreferences,
        WpfRealtimeOverlayOutput realtimeOutput)
    {
        ArgumentNullException.ThrowIfNull(popupModule);
        ArgumentNullException.ThrowIfNull(realtimeModule);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(monitorService);
        ArgumentNullException.ThrowIfNull(featureAccessService);
        ArgumentNullException.ThrowIfNull(commerce);
        ArgumentNullException.ThrowIfNull(privacyPreferences);
        ArgumentNullException.ThrowIfNull(desktopPreferences);
        ArgumentNullException.ThrowIfNull(realtimeOutput);

        _popupModule = popupModule;
        _realtimeModule = realtimeModule;
        _monitorService = monitorService;
        _featureAccessService = featureAccessService;
        _commerce = commerce;
        _privacyPreferences = privacyPreferences;
        _desktopPreferences = desktopPreferences;
        _realtimeOutput = realtimeOutput;
        InitializeComponent();
        InitializeHybridUiHost();
        ApplyDesktopPreferences(_desktopPreferences.Current);
        _moduleCount = modules.Modules.Count;
        _realtimeModule.Coordinator.StateChanged += RealtimeCoordinator_StateChanged;
        _realtimeModule.Coordinator.FrameProcessed += RealtimeCoordinator_FrameProcessed;
        _commerce.StateChanged += Commerce_StateChanged;
        _privacyPreferences.Changed += PrivacyPreferences_Changed;
        _desktopPreferences.Changed += DesktopPreferences_Changed;
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

    private void DesktopPreferences_Changed(object? sender, DesktopPreferences preferences) =>
        Dispatcher.BeginInvoke(() =>
        {
            ApplyDesktopPreferences(preferences);
            if (_hwndSource is not null &&
                !string.Equals(_registeredPopupShortcut, preferences.PopupShortcut, StringComparison.OrdinalIgnoreCase))
            {
                _ = TryApplyPopupHotkey(preferences.PopupShortcut);
            }

            PublishDesktopSnapshot();
        });

    private void ApplyDesktopPreferences(DesktopPreferences preferences)
    {
        _sourceLanguage = ValidateLanguage(preferences.SourceLanguage, allowAuto: true);
        _targetLanguage = ValidateLanguage(preferences.TargetLanguage, allowAuto: false);
        PublishDesktopSnapshot();
    }

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

        // quy định ẩn app khi chụp màn hình
        // _ = SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
        _ = TryApplyPopupHotkey(_desktopPreferences.Current.PopupShortcut);
        _status = _hotkeyRegistered
            ? $"Ready. Highlight text in another app and press {_registeredPopupShortcut}."
            : $"{_desktopPreferences.Current.PopupShortcut} is used by another app. Quick text remains available.";
    }

    private bool TryApplyPopupHotkey(string shortcut)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !TryParseShortcut(shortcut, out var modifiers, out var virtualKey))
        {
            _hotkeyStatus = $"{shortcut} unavailable";
            return false;
        }

        var previousShortcut = _registeredPopupShortcut;
        var previousWasRegistered = _hotkeyRegistered;
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(handle, HotkeyId);
            _hotkeyRegistered = false;
        }

        if (RegisterHotKey(handle, HotkeyId, modifiers | ModNoRepeat, virtualKey))
        {
            _registeredPopupShortcut = shortcut;
            _hotkeyRegistered = true;
            _hotkeyStatus = $"{shortcut} ready";
            return true;
        }

        if (previousWasRegistered &&
            TryParseShortcut(previousShortcut, out var previousModifiers, out var previousVirtualKey))
        {
            _hotkeyRegistered = RegisterHotKey(
                handle,
                HotkeyId,
                previousModifiers | ModNoRepeat,
                previousVirtualKey);
        }

        _hotkeyStatus = $"{shortcut} unavailable";
        return false;
    }

    private static bool TryParseShortcut(
        string? shortcut,
        out uint modifiers,
        out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        var parts = shortcut?
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is null or { Length: 0 })
        {
            return false;
        }

        for (var index = 0; index < parts.Length - 1; index++)
        {
            switch (parts[index].ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModControl;
                    break;
                case "alt":
                    modifiers |= ModAlt;
                    break;
                case "shift":
                    modifiers |= ModShift;
                    break;
                case "win":
                case "windows":
                    modifiers |= ModWindows;
                    break;
                default:
                    return false;
            }
        }

        var keyText = parts[^1] switch
        {
            var value when value.Equals("Esc", StringComparison.OrdinalIgnoreCase) => "Escape",
            var value when value.Equals("Enter", StringComparison.OrdinalIgnoreCase) => "Return",
            var value when value.Equals("Backspace", StringComparison.OrdinalIgnoreCase) => "Back",
            _ => parts[^1]
        };
        if (!Enum.TryParse<Key>(keyText, ignoreCase: true, out var key) || key == Key.None)
        {
            return false;
        }

        var nativeKey = KeyInterop.VirtualKeyFromKey(key);
        if (nativeKey <= 0)
        {
            return false;
        }

        virtualKey = (uint)nativeKey;
        return true;
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
        var preferences = _desktopPreferences.Current;
        var sourceLanguage = _sourceLanguage;
        var baseOptions = GetSelectedProcessingModeTag() switch
        {
            "subtitle" => RealtimeTranslationOptions.Subtitle,
            "game-dialogue" => RealtimeTranslationOptions.GameDialogue,
            "fullscreen" => RealtimeTranslationOptions.FullScreen,
            "comic" => RealtimeTranslationOptions.Comic,
            _ => RealtimeTranslationOptions.Document
        };

        return baseOptions with
        {
            SourceLanguage = sourceLanguage,
            TargetLanguage = _targetLanguage,
            OcrInterval = TimeSpan.FromMilliseconds(preferences.ScanIntervalMs),
            MaxOcrFragments = preferences.MaxOcrFragments,
            TranslationPrompt = CreatePromptOptions(preferences)
        };
    }

    private string GetSelectedProcessingModeTag() => _desktopPreferences.Current.OcrMode;

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
        var preferences = _desktopPreferences.Current;
        var popup = new TranslationPopupWindow(
            preferences.ClosePopupOnOutsideClick,
            preferences.AllowPopupDragging);

        _status = loadingStatus;
        PublishDesktopSnapshot();

        try
        {
            var operationTask = operation(cancellationToken);
            if (preferences.ShowPopupBeforeTranslationFinishes)
            {
                popup.Show();
            }

            var result = await operationTask;
            if (!result.HasSelection || result.Translation is null || result.SourceText is null)
            {
                popup.Close();
                _status = $"No highlighted text found. Select text and try {_desktopPreferences.Current.PopupShortcut} again.";
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
        catch (Exception exception)
        {
            new LocalExceptionLog().Report("popup-translation", exception);
            if (!popup.IsVisible)
            {
                popup.Show();
            }

            popup.ShowError(
                "Translation unavailable",
                GetPopupTranslationError(exception));
            _status = GetPopupTranslationError(exception);
        }
        finally
        {
            PublishDesktopSnapshot();
            if (ReferenceEquals(_popupCancellation, cancellation))
            {
                _popupCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private static string GetPopupTranslationError(Exception exception) => exception switch
    {
        Win32Exception =>
            "Could not copy the selected text. Select text in an app running with the same permissions as LexVerse and try again.",
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } =>
            "Your session has expired. Sign out and sign in again.",
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } =>
            "Your account does not have access to this translation feature. Check your plan.",
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            "The translation quota or request limit has been reached. Check your plan and try again later.",
        HttpRequestException { StatusCode: HttpStatusCode.BadGateway } =>
            "The server could not complete the translation with its provider (HTTP 502). Check the server translation logs.",
        HttpRequestException { StatusCode: HttpStatusCode.GatewayTimeout } =>
            "The translation provider timed out (HTTP 504). Check the server logs before retrying.",
        HttpRequestException { StatusCode: { } status } =>
            $"The server rejected the translation (HTTP {(int)status}). Check the server logs.",
        HttpRequestException =>
            "Cannot connect to the translation server. Check your network connection and server address.",
        OperationCanceledException =>
            "The translation request timed out. Check the server logs before retrying.",
        _ => "Translation failed. Check your sign-in, desktop configuration, and local app error log."
    };

    private PopupTranslationRequest CreateRequest()
    {
        var preferences = _desktopPreferences.Current;
        var sourceLanguage = _sourceLanguage;
        return new PopupTranslationRequest(
            _targetLanguage,
            sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? null
                : sourceLanguage,
            CreatePromptOptions(preferences));
    }

    private static TranslationPromptOptions CreatePromptOptions(DesktopPreferences preferences) =>
        new(
            preferences.TranslationInstruction,
            preferences.IgnoredTerms.Split(
                [',', ';', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            preferences.PreserveAcronymsAndTechnicalTerms);

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
        _desktopPreferences.Changed -= DesktopPreferences_Changed;
        base.OnClosed(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
}
