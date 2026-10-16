using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using LexVerse.Application.Desktop;
using LexVerse.Application.Privacy;
using Microsoft.Web.WebView2.Core;

namespace LexVerse.App;

public partial class MainWindow
{
    private const string DesktopUiHostName = "app.lexverse.local";
    private const string DesktopUiOrigin = "https://app.lexverse.local";
    private readonly SemaphoreSlim _desktopCommandGate = new(1, 1);
    private CancellationTokenSource? _desktopUiLifetime;
    private DispatcherTimer? _desktopSnapshotTimer;
    private DispatcherTimer? _startupTimer;
    private bool _startupFailed;
    private bool _snapshotRequested;
    private bool _desktopUiReady;
    private bool _desktopSnapshotPending;
    private double? _lastFrameMilliseconds;
    private bool _lastFrameBudgetExceeded;
    private string _lastQuickTranslation = string.Empty;

    private void InitializeHybridUiHost()
    {
        _desktopUiLifetime = new CancellationTokenSource();
        _desktopSnapshotTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _desktopSnapshotTimer.Tick += DesktopSnapshotTimer_Tick;
        WebUi.Visibility = Visibility.Hidden;
    }

    private async Task InitializeWebUiAsync()
    {
        var lifetime = _desktopUiLifetime;
        if (lifetime is null || lifetime.IsCancellationRequested)
        {
            return;
        }

        var token = lifetime.Token;
        _startupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _startupTimer.Tick += (_, _) => ShowStartupError("The workspace did not become ready within 30 seconds. Close LexVerse and try again.");
        _startupTimer.Start();
        try
        {
            var assetsPath = Path.Combine(AppContext.BaseDirectory, "DesktopUi");
            if (!File.Exists(Path.Combine(assetsPath, "index.html")))
                throw new FileNotFoundException("The packaged desktop UI is missing.");
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LexVerse",
                "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            token.ThrowIfCancellationRequested();
            await WebUi.EnsureCoreWebView2Async(environment);
            token.ThrowIfCancellationRequested();

            var core = WebUi.CoreWebView2;
            ConfigureWebViewSecurity(core);
            core.SetVirtualHostNameToFolderMapping(
                DesktopUiHostName,
                assetsPath,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += WebUi_WebMessageReceived;
            core.NavigationCompleted += WebUi_NavigationCompleted;
            core.Navigate($"{DesktopUiOrigin}/index.html");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
                ShowStartupError(exception switch
                {
                    WebView2RuntimeNotFoundException => "WebView2 Runtime is unavailable. Install Microsoft Edge WebView2 Runtime and restart LexVerse.",
                    FileNotFoundException => "The packaged desktop UI is missing. Rebuild or reinstall LexVerse.",
                    _ => "The workspace could not load. Close LexVerse and try again."
                });
        }
    }

    private void ShowStartupError(string message)
    {
        if (_cleanupStarted || _startupFailed) return;
        _startupFailed = true;
        _desktopUiReady = false;
        _startupTimer?.Stop();
        _desktopUiLifetime?.Cancel();
        WebUi.Visibility = Visibility.Collapsed;
        StartupLoadingPanel.Visibility = Visibility.Visible;
        StartupMessage.Text = message;
        StartupProgress.Visibility = Visibility.Collapsed;
        StartupCloseButton.Visibility = Visibility.Visible;
    }

    private void ConfigureWebViewSecurity(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.NavigationStarting += (_, args) =>
        {
            if (!IsTrustedDesktopUiUri(args.Uri))
            {
                args.Cancel = true;
            }
        };
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.DownloadStarting += (_, args) => args.Cancel = true;
        core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
    }

    private static bool IsTrustedDesktopUiUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && uri.Host.Equals(DesktopUiHostName, StringComparison.OrdinalIgnoreCase)
        && uri.IsDefaultPort;

    private void WebUi_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            ShowStartupError("The workspace page could not load. Close LexVerse and try again.");
        }
    }

    private async void WebUi_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!IsTrustedDesktopUiUri(args.Source))
        {
            return;
        }

        var json = args.WebMessageAsJson;
        if (!DesktopBridgeProtocol.TryParseCommand(json, out var command, out var parseError) || command is null)
        {
            PostDesktopResult(string.Empty, false, errorCode: parseError);
            return;
        }

        var lifetime = _desktopUiLifetime;
        if (lifetime is null || lifetime.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await _desktopCommandGate.WaitAsync(lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            var payload = await ExecuteDesktopCommandAsync(command);
            PostDesktopResult(command.Id, true, payload);
            PublishDesktopSnapshot();
        }
        catch (DesktopBridgePayloadException exception)
        {
            PostDesktopResult(command.Id, false, errorCode: exception.ErrorCode);
        }
        catch (JsonException)
        {
            PostDesktopResult(command.Id, false, errorCode: "invalid-payload");
        }
        catch (OperationCanceledException)
        {
            PostDesktopResult(command.Id, false, errorCode: "operation-cancelled");
        }
        catch
        {
            PostDesktopResult(command.Id, false, errorCode: "operation-failed");
        }
        finally
        {
            _desktopCommandGate.Release();
        }
    }

    private async Task<object?> ExecuteDesktopCommandAsync(DesktopBridgeCommand command)
    {
        if (_cleanupStarted || _startupFailed)
            throw new OperationCanceledException();
        switch (command.Type)
        {
            case "app.getSnapshot":
                _snapshotRequested = true;
                return BuildDesktopSnapshot();

            case "app.uiReady":
                if (!_snapshotRequested || _startupFailed || _cleanupStarted)
                    throw new DesktopBridgePayloadException("workspace-not-ready");
                _desktopUiReady = true;
                _startupTimer?.Stop();
                WebUi.Visibility = Visibility.Visible;
                StartupLoadingPanel.Visibility = Visibility.Collapsed;
                return null;

            case "translation.translateQuickText":
            {
                var payload = DesktopBridgeProtocol.DeserializePayload<QuickTranslationPayload>(command.Payload);
                if (string.IsNullOrWhiteSpace(payload.Text) || payload.Text.Length > 32_000)
                {
                    throw new DesktopBridgePayloadException("invalid-text");
                }

                _sourceLanguage = ValidateLanguage(payload.SourceLanguage, allowAuto: true);
                _targetLanguage = ValidateLanguage(payload.TargetLanguage, allowAuto: false);
                await TranslateQuickTextAsync(payload.Text);
                return null;
            }

            case "translation.triggerSelectedText":
                await RunSelectedTextPopupAsync();
                return null;

            case "realtime.selectRegion":
            case "realtime.start":
                await StartRegionRealtimeAsync();
                return null;

            case "realtime.startFullScreen":
                await StartFullScreenRealtimeAsync();
                return null;

            case "realtime.stop":
                await StopRealtimeAsync();
                return null;

            case "realtime.resetOverlay":
                await _realtimeOutput.ClearAsync();
                _status = "Realtime overlay cleared.";
                return null;

            case "privacy.updateConsent":
            {
                var payload = DesktopBridgeProtocol.DeserializePayload<PrivacyPayload>(command.Payload);
                await UpdatePrivacyFromWebAsync(payload);
                return null;
            }

            case "settings.update":
            {
                var payload = DesktopBridgeProtocol.DeserializePayload<SettingsPayload>(command.Payload);
                await UpdateDesktopSettingsAsync(payload);
                return null;
            }

            case "account.signIn":
                _commerce.BeginSignIn();
                return null;

            case "account.reopenSignIn":
                _commerce.ReopenSignIn();
                return null;

            case "account.copySignInLink":
                if (_commerce.PendingAuthorizationUri is { } authorizationUri)
                    Clipboard.SetText(authorizationUri.AbsoluteUri);
                return null;

            case "account.cancelSignIn":
                _commerce.CancelSignIn();
                return null;

            case "account.signOut":
                await RunCommerceOperationAsync(_commerce.SignOutAsync, "Signing out...");
                return null;

            case "commerce.restore":
                await RunCommerceOperationAsync(_commerce.RestorePurchasesAsync, "Restoring purchases and refreshing access...");
                return null;

            case "commerce.checkout":
            {
                var payload = DesktopBridgeProtocol.DeserializePayload<CheckoutPayload>(command.Payload);
                var offerExists = _commerce.State.Catalog?.Offers.Any(
                    offer => offer.Id.Equals(payload.OfferId, StringComparison.OrdinalIgnoreCase)) == true;
                if (string.IsNullOrWhiteSpace(payload.OfferId) || !offerExists)
                {
                    throw new DesktopBridgePayloadException("invalid-offer");
                }
                await RunCommerceOperationAsync(
                    token => _commerce.StartCheckoutAsync(payload.OfferId, token),
                    "Creating secure checkout...");
                return null;
            }

            case "external.openHelp":
                Process.Start(new ProcessStartInfo(
                    "https://github.com/thisishiu/LexVerse-Real-time-screen-translate")
                {
                    UseShellExecute = true
                });
                return null;

            default:
                throw new DesktopBridgePayloadException("unknown-command");
        }
    }

    private async Task UpdatePrivacyFromWebAsync(PrivacyPayload payload)
    {
        var current = _privacyPreferences.Current;
        if (payload.AllowRemoteTextProcessing && !current.AllowRemoteTextProcessing)
        {
            var confirmation = MessageBox.Show(
                "LexVerse will send selected, typed, or OCR text to the configured translation provider. Operational metrics never include that text. Continue?",
                "Allow external text processing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (confirmation != MessageBoxResult.Yes)
            {
                throw new DesktopBridgePayloadException("consent-declined");
            }
        }

        var updated = new PrivacyPreferences(
            payload.AllowRemoteTextProcessing,
            payload.AllowLocalDiagnostics);
        await _privacyPreferences.UpdateAsync(updated);
        if (!updated.AllowRemoteTextProcessing)
        {
            await _realtimeModule.Coordinator.StopAsync();
        }
        await RefreshFeatureAccessAsync();
        _status = "Privacy preferences saved.";
    }

    private async Task UpdateDesktopSettingsAsync(SettingsPayload payload)
    {
        var current = _desktopPreferences.Current;
        var shortcuts = payload.Shortcuts;
        var updated = (current with
        {
            AppLanguage = payload.AppLanguage ?? current.AppLanguage,
            SourceLanguage = payload.SourceLanguage ?? current.SourceLanguage,
            TargetLanguage = payload.TargetLanguage ?? current.TargetLanguage,
            OcrMode = payload.OcrMode ?? current.OcrMode,
            IgnoredTerms = payload.IgnoredTerms ?? current.IgnoredTerms,
            PreserveAcronymsAndTechnicalTerms = payload.PreserveAcronymsAndTechnicalTerms
                ?? current.PreserveAcronymsAndTechnicalTerms,
            ClosePopupOnOutsideClick = payload.ClosePopupOnOutsideClick
                ?? current.ClosePopupOnOutsideClick,
            AllowPopupDragging = payload.AllowPopupDragging ?? current.AllowPopupDragging,
            ShowOcrDebugBoxes = payload.ShowOcrDebugBoxes ?? current.ShowOcrDebugBoxes,
            ShowPopupBeforeTranslationFinishes = payload.ShowPopupBeforeTranslationFinishes
                ?? current.ShowPopupBeforeTranslationFinishes,
            ScanIntervalMs = payload.ScanIntervalMs ?? current.ScanIntervalMs,
            OverlayOpacityPercent = payload.OverlayOpacityPercent ?? current.OverlayOpacityPercent,
            OverlayFontScalePercent = payload.OverlayFontScalePercent ?? current.OverlayFontScalePercent,
            UseDarkOverlayBlocks = payload.UseDarkOverlayBlocks ?? current.UseDarkOverlayBlocks,
            MaxOcrFragments = payload.MaxOcrFragments ?? current.MaxOcrFragments,
            TranslationInstruction = payload.TranslationInstruction ?? current.TranslationInstruction,
            PopupShortcut = shortcuts?.Popup ?? current.PopupShortcut,
            RegionShortcut = shortcuts?.Region ?? current.RegionShortcut,
            FullScreenShortcut = shortcuts?.FullScreen ?? current.FullScreenShortcut,
            StopShortcut = shortcuts?.Stop ?? current.StopShortcut,
            ResetShortcut = shortcuts?.Reset ?? current.ResetShortcut
        }).Normalize();

        if (!AreShortcutsValid(updated))
        {
            throw new DesktopBridgePayloadException("invalid-shortcut");
        }

        var popupShortcutChanged = !string.Equals(
            current.PopupShortcut,
            updated.PopupShortcut,
            StringComparison.OrdinalIgnoreCase);
        if (popupShortcutChanged && _hwndSource is not null && !TryApplyPopupHotkey(updated.PopupShortcut))
        {
            throw new DesktopBridgePayloadException("shortcut-unavailable");
        }

        try
        {
            await _desktopPreferences.UpdateAsync(updated);
        }
        catch
        {
            if (popupShortcutChanged && _hwndSource is not null)
            {
                _ = TryApplyPopupHotkey(current.PopupShortcut);
            }

            throw;
        }

        ApplyDesktopPreferences(updated);
        _status = "Application settings saved.";
    }

    private static bool AreShortcutsValid(DesktopPreferences preferences) =>
        TryParseShortcut(preferences.PopupShortcut, out _, out _) &&
        TryParseShortcut(preferences.RegionShortcut, out _, out _) &&
        TryParseShortcut(preferences.FullScreenShortcut, out _, out _) &&
        TryParseShortcut(preferences.StopShortcut, out _, out _) &&
        TryParseShortcut(preferences.ResetShortcut, out _, out _);

    private static string ValidateLanguage(string language, bool allowAuto)
    {
        var normalized = language?.ToLowerInvariant();
        if (normalized is "en" or "ja" or "ko" or "zh" or "vi" || (allowAuto && normalized == "auto"))
            return normalized;
        throw new DesktopBridgePayloadException("invalid-language");
    }

    private object BuildDesktopSnapshot()
    {
        var commerce = _commerce.State;
        var catalog = commerce.Catalog;
        var offers = (catalog?.Offers ?? Enumerable.Empty<LexVerse.Core.Product.ProductOffer>())
            .Where(offer => offer.IsPublic && catalog?.FindPlan(offer.PlanId) is { IsPublic: true })
            .OrderBy(offer => offer.Price.Amount)
            .Select(offer => new
            {
                id = offer.Id,
                label = $"{catalog!.FindPlan(offer.PlanId)!.DisplayName} · {offer.Price.Amount:N0} {offer.Price.CurrencyCode} · " +
                    (offer.BillingPeriod is null ? "one-time" : $"every {offer.BillingPeriod.Count} {offer.BillingPeriod.Unit.ToString().ToLowerInvariant()}")
            }).ToArray();
        var realtime = _realtimeModule.Coordinator.State;
        var preferences = _desktopPreferences.Current;
        return new
        {
            moduleCount = _moduleCount,
            status = _status,
            hotkey = _hotkeyStatus,
            quickTranslation = _lastQuickTranslation,
            sourceLanguage = _sourceLanguage,
            targetLanguage = _targetLanguage,
            settings = new
            {
                appLanguage = preferences.AppLanguage,
                sourceLanguage = preferences.SourceLanguage,
                targetLanguage = preferences.TargetLanguage,
                ocrMode = preferences.OcrMode,
                ignoredTerms = preferences.IgnoredTerms,
                preserveAcronymsAndTechnicalTerms = preferences.PreserveAcronymsAndTechnicalTerms,
                closePopupOnOutsideClick = preferences.ClosePopupOnOutsideClick,
                allowPopupDragging = preferences.AllowPopupDragging,
                showOcrDebugBoxes = preferences.ShowOcrDebugBoxes,
                showPopupBeforeTranslationFinishes = preferences.ShowPopupBeforeTranslationFinishes,
                scanIntervalMs = preferences.ScanIntervalMs,
                overlayOpacityPercent = preferences.OverlayOpacityPercent,
                overlayFontScalePercent = preferences.OverlayFontScalePercent,
                useDarkOverlayBlocks = preferences.UseDarkOverlayBlocks,
                maxOcrFragments = preferences.MaxOcrFragments,
                translationInstruction = preferences.TranslationInstruction,
                shortcuts = new
                {
                    popup = preferences.PopupShortcut,
                    region = preferences.RegionShortcut,
                    fullScreen = preferences.FullScreenShortcut,
                    stop = preferences.StopShortcut,
                    reset = preferences.ResetShortcut
                }
            },
            privacy = new
            {
                allowRemoteTextProcessing = _privacyPreferences.Current.AllowRemoteTextProcessing,
                allowLocalDiagnostics = _privacyPreferences.Current.AllowLocalDiagnostics
            },
            realtime = new
            {
                status = realtime.Status.ToString().ToLowerInvariant(),
                canStartRegion = CanStartRegion,
                canStartFullScreen = CanStartFullScreen,
                canStop = IsRealtimeRunning,
                lastFrameMilliseconds = _lastFrameMilliseconds,
                performanceBudgetExceeded = _lastFrameBudgetExceeded
            },
            commerce = new
            {
                configured = commerce.Status != LexVerse.Application.Commerce.CommerceStatus.Unconfigured,
                status = commerce.Status switch
                {
                    LexVerse.Application.Commerce.CommerceStatus.Unconfigured => "FREE MODE",
                    LexVerse.Application.Commerce.CommerceStatus.SignedOut => "SIGNED OUT",
                    LexVerse.Application.Commerce.CommerceStatus.Ready => "SIGNED IN",
                    LexVerse.Application.Commerce.CommerceStatus.CheckoutOpened => "CHECKOUT",
                    LexVerse.Application.Commerce.CommerceStatus.Error => "RETRY",
                    _ => "LOADING"
                },

                displayName = commerce.Account?.DisplayName,
                authentication = _commerce.Authentication,
                offers
            }
        };
    }

    private void PostDesktopResult(string id, bool ok, object? payload = null, string? errorCode = null)
    {
        if (_cleanupStarted || _startupFailed || WebUi.CoreWebView2 is null)
        {
            return;
        }
        WebUi.CoreWebView2.PostWebMessageAsJson(
            DesktopBridgeProtocol.SerializeResult(id, ok, payload, errorCode));
    }

    private void PublishDesktopSnapshot()
    {
        if (!_desktopUiReady || WebUi.CoreWebView2 is null || _desktopSnapshotTimer is null)
        {
            return;
        }

        _desktopSnapshotPending = true;
        if (!_desktopSnapshotTimer.IsEnabled)
        {
            _desktopSnapshotTimer.Start();
        }
    }

    private void DesktopSnapshotTimer_Tick(object? sender, EventArgs args)
    {
        _desktopSnapshotTimer?.Stop();
        if (!_desktopSnapshotPending || !_desktopUiReady || WebUi.CoreWebView2 is null)
        {
            return;
        }

        _desktopSnapshotPending = false;
        WebUi.CoreWebView2.PostWebMessageAsJson(
            DesktopBridgeProtocol.SerializeEvent("app.snapshot", BuildDesktopSnapshot()));
    }

    private void UpdateDesktopFrameTiming(double milliseconds, bool exceeded)
    {
        _lastFrameMilliseconds = milliseconds;
        _lastFrameBudgetExceeded = exceeded;
    }

    private void ShutdownHybridUiHost()
    {
        _desktopUiReady = false;
        _startupTimer?.Stop();
        _startupTimer = null;
        _desktopUiLifetime?.Cancel();
        _desktopUiLifetime?.Dispose();
        _desktopUiLifetime = null;
        if (_desktopSnapshotTimer is not null)
        {
            _desktopSnapshotTimer.Stop();
            _desktopSnapshotTimer.Tick -= DesktopSnapshotTimer_Tick;
            _desktopSnapshotTimer = null;
        }
        _desktopSnapshotPending = false;
        if (WebUi.CoreWebView2 is not null)
        {
            WebUi.CoreWebView2.WebMessageReceived -= WebUi_WebMessageReceived;
            WebUi.CoreWebView2.NavigationCompleted -= WebUi_NavigationCompleted;
        }
        WebUi.Dispose();
    }

    private sealed record QuickTranslationPayload(
        string Text,
        string SourceLanguage,
        string TargetLanguage);

    private sealed record PrivacyPayload(
        bool AllowRemoteTextProcessing,
        bool AllowLocalDiagnostics);

    private sealed record SettingsPayload(
        string? AppLanguage = null,
        string? SourceLanguage = null,
        string? TargetLanguage = null,
        string? OcrMode = null,
        string? IgnoredTerms = null,
        bool? PreserveAcronymsAndTechnicalTerms = null,
        bool? ClosePopupOnOutsideClick = null,
        bool? AllowPopupDragging = null,
        bool? ShowOcrDebugBoxes = null,
        bool? ShowPopupBeforeTranslationFinishes = null,
        int? ScanIntervalMs = null,
        int? OverlayOpacityPercent = null,
        int? OverlayFontScalePercent = null,
        bool? UseDarkOverlayBlocks = null,
        int? MaxOcrFragments = null,
        string? TranslationInstruction = null,
        ShortcutPayload? Shortcuts = null);

    private sealed record ShortcutPayload(
        string? Popup = null,
        string? Region = null,
        string? FullScreen = null,
        string? Stop = null,
        string? Reset = null);

    private sealed record CheckoutPayload(string OfferId);
}
