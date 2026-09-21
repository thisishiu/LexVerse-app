using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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

        try
        {
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LexVerse",
                "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await WebUi.EnsureCoreWebView2Async(environment);
            lifetime.Token.ThrowIfCancellationRequested();

            var core = WebUi.CoreWebView2;
            ConfigureWebViewSecurity(core);
            var assetsPath = Path.Combine(AppContext.BaseDirectory, "DesktopUi");
            if (!File.Exists(Path.Combine(assetsPath, "index.html")))
            {
                throw new FileNotFoundException("The packaged desktop UI is missing.");
            }

            core.SetVirtualHostNameToFolderMapping(
                DesktopUiHostName,
                assetsPath,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += WebUi_WebMessageReceived;
            core.NavigationCompleted += WebUi_NavigationCompleted;
            core.Navigate($"{DesktopUiOrigin}/index.html");
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException
                                          or IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException)
        {
            WebUi.Visibility = Visibility.Collapsed;
            StatusText.Text = exception is WebView2RuntimeNotFoundException
                ? "WebView2 Runtime is unavailable. Using the native control center."
                : "The web control center could not load. Using the native fallback.";
        }
    }

    private void ConfigureWebViewSecurity(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
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
            WebUi.Visibility = Visibility.Collapsed;
            StatusText.Text = "The web control center could not load. Using the native fallback.";
            return;
        }

        _desktopUiReady = true;
        WebUi.Visibility = Visibility.Visible;
        PublishDesktopSnapshot();
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
        switch (command.Type)
        {
            case "app.getSnapshot":
                return BuildDesktopSnapshot();

            case "translation.translateQuickText":
            {
                var payload = DesktopBridgeProtocol.DeserializePayload<QuickTranslationPayload>(command.Payload);
                if (string.IsNullOrWhiteSpace(payload.Text) || payload.Text.Length > 32_000)
                {
                    throw new DesktopBridgePayloadException("invalid-text");
                }

                SelectLanguage(SourceLanguageBox, payload.SourceLanguage, allowAuto: true);
                SelectLanguage(TargetLanguageBox, payload.TargetLanguage, allowAuto: false);
                QuickTextBox.Text = payload.Text;
                await TranslateQuickTextAsync();
                return null;
            }

            case "realtime.selectRegion":
            case "realtime.start":
                await StartRegionRealtimeAsync();
                return null;

            case "realtime.stop":
                await StopRealtimeAsync();
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
                if (payload.SourceLanguage is not null)
                {
                    SelectLanguage(SourceLanguageBox, payload.SourceLanguage, allowAuto: true);
                }
                if (payload.TargetLanguage is not null)
                {
                    SelectLanguage(TargetLanguageBox, payload.TargetLanguage, allowAuto: false);
                }
                return null;
            }

            case "account.signIn":
                await RunCommerceOperationAsync(_commerce.SignInAsync, "Opening secure sign-in in your browser...");
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
        StatusText.Text = "Privacy preferences saved.";
    }

    private static void SelectLanguage(ComboBox comboBox, string language, bool allowAuto)
    {
        if (string.IsNullOrWhiteSpace(language) ||
            (!allowAuto && language.Equals("auto", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DesktopBridgePayloadException("invalid-language");
        }

        var item = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(candidate => string.Equals(
                candidate.Tag?.ToString(),
                language,
                StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            throw new DesktopBridgePayloadException("invalid-language");
        }

        comboBox.SelectedItem = item;
    }

    private object BuildDesktopSnapshot()
    {
        var commerce = _commerce.State;
        var offers = OfferBox.Items
            .OfType<ComboBoxItem>()
            .Where(item => item.Tag is string)
            .Select(item => new
            {
                id = item.Tag!.ToString(),
                label = item.Content?.ToString() ?? item.Tag!.ToString()
            })
            .ToArray();
        var realtime = _realtimeModule.Coordinator.State;
        return new
        {
            moduleCount = 2,
            status = StatusText.Text,
            hotkey = HotkeyStatusText.Text,
            quickTranslation = _lastQuickTranslation,
            sourceLanguage = GetSelectedTag(SourceLanguageBox),
            targetLanguage = GetSelectedTag(TargetLanguageBox),
            privacy = new
            {
                allowRemoteTextProcessing = _privacyPreferences.Current.AllowRemoteTextProcessing,
                allowLocalDiagnostics = _privacyPreferences.Current.AllowLocalDiagnostics
            },
            realtime = new
            {
                status = realtime.Status.ToString().ToLowerInvariant(),
                canStartRegion = RegionButton.IsEnabled,
                canStop = StopRealtimeButton.IsEnabled,
                lastFrameMilliseconds = _lastFrameMilliseconds,
                performanceBudgetExceeded = _lastFrameBudgetExceeded
            },
            commerce = new
            {
                configured = commerce.Status != LexVerse.Application.Commerce.CommerceStatus.Unconfigured,
                status = AccountStatusText.Text,
                displayName = commerce.Account?.DisplayName,
                offers
            }
        };
    }

    private void PostDesktopResult(string id, bool ok, object? payload = null, string? errorCode = null)
    {
        if (WebUi.CoreWebView2 is null)
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
        string? SourceLanguage = null,
        string? TargetLanguage = null);

    private sealed record CheckoutPayload(string OfferId);
}
