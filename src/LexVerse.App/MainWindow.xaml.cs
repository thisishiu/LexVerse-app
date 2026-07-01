using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using LexVerse.Core.Models;
using LexVerse.Core.Services;
using LexVerse.Infrastructure.Bedrock;
using LexVerse.Infrastructure.Configuration;
using LexVerse.Infrastructure.Google;
using LexVerse.Infrastructure.Ocr;
using LexVerse.Infrastructure.Settings;
using LexVerse.Infrastructure.Translation;

namespace LexVerse.App;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4C56;
    private const int WmHotkey = 0x0312;
    private const uint VkF6 = 0x75;
    private const uint ModNoRepeat = 0x4000;

    private readonly ITranslationService _translationService;
    private readonly Lazy<ITranslationService> _realtimeTranslationService;
    private readonly ITextSegmentationService _textSegmentationService;
    private readonly AppSettings _settings;
    private HwndSource? _hwndSource;
    private CancellationTokenSource? _realtimeCancellation;
    private RealtimeOverlayWindow? _realtimeOverlay;
    private bool _isSelecting;

    public MainWindow()
    {
        InitializeComponent();

        var envPath = ProjectFileLocator.FindUpward(".env") ?? Path.Combine(AppContext.BaseDirectory, ".env");
        var env = EnvFileLoader.Load(envPath);
        var httpClient = new HttpClient();
        var settingsStore = new EnvSettingsStore(envPath);
        var bedrockSettings = BedrockSettings.FromEnv(env);
        var googleCredentialPath = ResolveCredentialPath(env.GetValueOrDefault("GOOGLE_APPLICATION_CREDENTIALS", "lexverse-42d205709f8e.json"), envPath);

        _settings = settingsStore.Load();
        _translationService = new BedrockPopupTranslationService(httpClient, bedrockSettings);
        _realtimeTranslationService = new Lazy<ITranslationService>(() =>
            new GoogleTranslateService(httpClient, new GoogleAccessTokenProvider(httpClient, googleCredentialPath)));
        _textSegmentationService = new WindowsOcrTextSegmentationService();

        KeyDown += MainWindow_KeyDown;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        ApplySettings();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private void ApplySettings()
    {
        SelectByTag(SourceLanguageBox, _settings.SourceLanguage);
        SelectByTag(TargetLanguageBox, _settings.TargetLanguage);
        IgnoredTermsBox.Text = string.Join(", ", _settings.IgnoredTerms);
        MovablePopupCheck.IsChecked = _settings.AllowFreeOverlayMovement;
        RealtimeIntervalSlider.Value = _settings.RealtimeScanIntervalMs;
        RealtimeOpacitySlider.Value = _settings.RealtimeOverlayOpacity;
        RealtimeFontSizeSlider.Value = _settings.RealtimeFontSize;
        RealtimeMaxSegmentsSlider.Value = _settings.RealtimeMaxSegments;
    }

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private async void ActivatePopup_Click(object sender, RoutedEventArgs e)
    {
        await StartAreaTranslationAsync();
    }

    private async void ShowDemo_Click(object sender, RoutedEventArgs e)
    {
        var selection = new ScreenSelection((int)Left + 450, (int)Top + 220, 620, 180);
        await TranslateAndShowPopupAsync(selection, "AI helps me learn English faster every day.");
    }

    private void Realtime_Click(object sender, RoutedEventArgs e)
    {
        _ = StartRealtimeAsync();
    }

    private async void StartRealtime_Click(object sender, RoutedEventArgs e)
    {
        await StartRealtimeAsync();
    }

    private void StopRealtime_Click(object sender, RoutedEventArgs e)
    {
        StopRealtime("Realtime stopped.");
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        var registered = RegisterHotKey(handle, HotkeyId, ModNoRepeat, VkF6);
        StatusText.Text = registered
            ? "Status: Ready. F6 works globally while Lexverse is running."
            : "Status: Ready, but F6 global hotkey is already used by another app.";
        HotkeyStatusText.Text = registered ? "F6 ready" : "F6 is used by another app";
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HotkeyId);
        _hwndSource?.RemoveHook(WndProc);
        StopRealtime("Realtime stopped.");
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _ = StartAreaTranslationAsync();
        }

        return IntPtr.Zero;
    }

    private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F6)
        {
            await StartAreaTranslationAsync();
        }
    }

    private async Task StartAreaTranslationAsync()
    {
        if (_isSelecting)
        {
            return;
        }

        _isSelecting = true;
        StatusText.Text = "Status: Select a screen area to translate.";

        try
        {
            WindowState = WindowState.Minimized;

            var selector = new SelectionOverlayWindow();
            var completed = selector.ShowDialog();

            if (completed != true || selector.Selection is not { IsMeaningful: true } selection)
            {
                WindowState = WindowState.Normal;
                Activate();
                StatusText.Text = "Status: Selection cancelled.";
                return;
            }

            await TranslateSelectionAndShowPopupAsync(selection);
        }
        catch (Exception exception)
        {
            StatusText.Text = "Status: Translation failed. Check Bedrock access, model capability, and selected text.";
            MessageBox.Show(
                this,
                exception.Message,
                "Lexverse could not translate this area",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _isSelecting = false;
        }
    }

    private async Task TranslateAndShowPopupAsync(ScreenSelection selection, string sourceText)
    {
        StatusText.Text = "Status: Translating selected text.";

        var request = new TranslationRequest(
            sourceText,
            SelectedLanguage(SourceLanguageBox, _settings.SourceLanguage),
            SelectedLanguage(TargetLanguageBox, _settings.TargetLanguage),
            selection);

        var result = await _translationService.TranslateAsync(request);
        var popup = new TranslationPopupWindow(result, selection);
        popup.Show();

        StatusText.Text = $"Status: Translated \"{TrimForStatus(sourceText)}\".";
    }

    private async Task TranslateSelectionAndShowPopupAsync(ScreenSelection selection)
    {
        StatusText.Text = "Status: Sending selected image to Bedrock.";
        var stopwatch = Stopwatch.StartNew();
        var popup = TranslationPopupWindow.CreateLoading(selection);
        popup.Show();
        var textSegments = await TryDetectTextSegmentsAsync(selection);

        var request = new TranslationRequest(
            SourceText: string.Empty,
            SourceLanguage: SelectedLanguage(SourceLanguageBox, _settings.SourceLanguage),
            TargetLanguage: SelectedLanguage(TargetLanguageBox, _settings.TargetLanguage),
            Selection: selection,
            TextSegments: textSegments);

        try
        {
            var result = await _translationService.TranslateAsync(request);
            popup.ShowResult(result);
            StatusText.Text = $"Status: Translated \"{TrimForStatus(result.SourceText)}\" in {stopwatch.Elapsed.TotalSeconds:0.0}s.";
        }
        catch (Exception exception)
        {
            popup.ShowError(exception.Message);
            StatusText.Text = "Status: Translation failed. Check Bedrock access, model capability, and selected text.";
        }
    }

    private async Task StartRealtimeAsync()
    {
        if (_isSelecting)
        {
            return;
        }

        StopRealtime("Restarting realtime.");
        _isSelecting = true;
        StatusText.Text = "Status: Select a realtime region.";
        RealtimeStatusText.Text = "Selecting region...";

        try
        {
            WindowState = WindowState.Minimized;

            var selector = new SelectionOverlayWindow();
            var completed = selector.ShowDialog();

            WindowState = WindowState.Normal;
            Activate();

            if (completed != true || selector.Selection is not { IsMeaningful: true } selection)
            {
                RealtimeStatusText.Text = "Selection cancelled.";
                StatusText.Text = "Status: Realtime selection cancelled.";
                return;
            }

            _realtimeCancellation = new CancellationTokenSource();
            _realtimeOverlay = new RealtimeOverlayWindow(selection);
            _realtimeOverlay.Show();
            _realtimeOverlay.UpdateText("Reading...", RealtimeOpacity(), RealtimeFontSize());

            RealtimeButton.IsEnabled = false;
            StopRealtimeButton.IsEnabled = true;
            RealtimeStatusText.Text = $"Watching {selection.Width}x{selection.Height}.";
            StatusText.Text = "Status: Realtime translation is running.";

            _ = RunRealtimeLoopAsync(selection, _realtimeCancellation.Token);
        }
        catch (Exception exception)
        {
            StopRealtime("Realtime failed.");
            MessageBox.Show(
                this,
                exception.Message,
                "Lexverse realtime could not start",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _isSelecting = false;
        }
    }

    private async Task RunRealtimeLoopAsync(ScreenSelection selection, CancellationToken cancellationToken)
    {
        string? lastSourceText = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _realtimeOverlay?.ShowReading();
                var segments = await _textSegmentationService.DetectTextSegmentsAsync(selection, cancellationToken);
                var sourceText = BuildRealtimeSourceText(segments);

                if (string.IsNullOrWhiteSpace(sourceText))
                {
                    _realtimeOverlay?.UpdateText("No readable text.", RealtimeOpacity(), RealtimeFontSize());
                    RealtimeStatusText.Text = "No readable text in region.";
                }
                else if (!string.Equals(sourceText, lastSourceText, StringComparison.Ordinal))
                {
                    lastSourceText = sourceText;
                    var request = new TranslationRequest(
                        sourceText,
                        SelectedLanguage(SourceLanguageBox, _settings.SourceLanguage),
                        SelectedLanguage(TargetLanguageBox, _settings.TargetLanguage));

                    var result = await _realtimeTranslationService.Value.TranslateAsync(request, cancellationToken);
                    _realtimeOverlay?.UpdateText(result.TranslatedText, RealtimeOpacity(), RealtimeFontSize());
                    RealtimeStatusText.Text = $"Updated {DateTime.Now:HH:mm:ss}.";
                    StatusText.Text = $"Status: Realtime translated \"{TrimForStatus(sourceText)}\".";
                }
                else
                {
                    RealtimeStatusText.Text = $"No text change {DateTime.Now:HH:mm:ss}.";
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _realtimeOverlay?.UpdateText(exception.Message, RealtimeOpacity(), RealtimeFontSize());
                RealtimeStatusText.Text = "Realtime provider error.";
                StatusText.Text = "Status: Realtime translation failed.";
            }

            try
            {
                await Task.Delay(RealtimeInterval(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void StopRealtime(string message)
    {
        _realtimeCancellation?.Cancel();
        _realtimeCancellation?.Dispose();
        _realtimeCancellation = null;
        _realtimeOverlay?.Close();
        _realtimeOverlay = null;

        if (IsLoaded)
        {
            RealtimeButton.IsEnabled = true;
            StopRealtimeButton.IsEnabled = false;
            RealtimeStatusText.Text = message;
        }
    }

    private string BuildRealtimeSourceText(IReadOnlyList<ScreenTextSegment> segments)
    {
        var maxSegments = Math.Clamp((int)Math.Round(RealtimeMaxSegmentsSlider.Value), 1, 30);
        var ignoredTerms = IgnoredTermsBox.Text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return string.Join(
            Environment.NewLine,
            segments
                .OrderBy(segment => segment.Bounds.Y)
                .ThenBy(segment => segment.Bounds.X)
                .Select(segment => segment.Text.Trim())
                .Where(text => text.Length > 0 && !ignoredTerms.Contains(text))
                .Take(maxSegments));
    }

    private async Task<IReadOnlyList<ScreenTextSegment>> TryDetectTextSegmentsAsync(ScreenSelection selection)
    {
        try
        {
            StatusText.Text = "Status: Reading selected text with Windows OCR.";
            return await _textSegmentationService.DetectTextSegmentsAsync(selection);
        }
        catch
        {
            StatusText.Text = "Status: Windows OCR was unavailable; falling back to Bedrock image reading.";
            return [];
        }
    }

    private static string SelectedLanguage(ComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
    }

    private static string TrimForStatus(string text)
    {
        return text.Length <= 46 ? text : string.Concat(text.AsSpan(0, 43), "...");
    }

    private int RealtimeInterval()
    {
        return Math.Clamp((int)Math.Round(RealtimeIntervalSlider.Value), 500, 5000);
    }

    private double RealtimeOpacity()
    {
        return Math.Clamp(RealtimeOpacitySlider.Value, 0.45, 1);
    }

    private double RealtimeFontSize()
    {
        return Math.Clamp(RealtimeFontSizeSlider.Value, 11, 30);
    }

    private static string ResolveCredentialPath(string credentialPath, string envPath)
    {
        if (Path.IsPathRooted(credentialPath))
        {
            return credentialPath;
        }

        var envDirectory = Path.GetDirectoryName(envPath);
        return Path.Combine(string.IsNullOrWhiteSpace(envDirectory) ? AppContext.BaseDirectory : envDirectory, credentialPath);
    }
}
