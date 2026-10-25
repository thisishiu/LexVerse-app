using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LexVerse.App.Demo;

internal sealed class SampleAppWindow : Window
{
    private readonly WebView2 _view = new();
    private readonly string _caseId;
    private readonly string _pipe;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherTimer _parentWatch = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly int? _parent;

    public SampleAppWindow(string caseId, string pipe, int? parent = null)
    {
        _caseId = caseId; _pipe = pipe; _parent = parent;
        Title = caseId switch { "article" => "Fieldnotes Reader", "game" => "Starfall", "letters" => "Postbox", "comic" => "After Rain Reader", _ => throw new ArgumentException("Unknown application.") };
        Width = 1240; Height = 860; MinWidth = 780; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = CreateIcon(caseId);
        Content = _view;
        Loaded += Initialize;
        Closed += (_, _) => { _parentWatch.Stop(); _lifetime.Cancel(); _ready.TrySetCanceled(); _view.Dispose(); };
        _parentWatch.Tick += (_, _) =>
        {
            if (_parent is not { } id) return;
            try { using var process = Process.GetProcessById(id); if (process.HasExited) Close(); }
            catch (ArgumentException) { Close(); }
        };
    }

    private async void Initialize(object sender, RoutedEventArgs args)
    {
        Loaded -= Initialize;
        try
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "DesktopUi");
            if (!File.Exists(Path.Combine(folder, "sample.html"))) throw new FileNotFoundException("Application content is missing.");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder:
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FieldnotesTools", "WebView", _pipe));
            if (_lifetime.IsCancellationRequested) return;
            await _view.EnsureCoreWebView2Async(environment);
            if (_lifetime.IsCancellationRequested) return;
            var core = _view.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.SetVirtualHostNameToFolderMapping("content.local", folder, CoreWebView2HostResourceAccessKind.DenyCors);
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) => { if (!IsLocal(e.Request.Uri)) e.Response = environment.CreateWebResourceResponse(null, 403, "Offline", ""); };
            core.NavigationStarting += (_, e) => e.Cancel = !IsLocal(e.Uri);
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NavigationCompleted += async (_, e) =>
            {
                if (!e.IsSuccess) { _ready.TrySetException(new IOException("Cannot load application content.")); return; }
                // The module has no native dependencies, but wait for its handler before accepting commands.
                for (var retry = 0; retry < 100 && !_lifetime.IsCancellationRequested; retry++)
                {
                    if (await core.ExecuteScriptAsync("typeof window.sampleCommand === 'function'") == "true")
                    { _ready.TrySetResult(); return; }
                    await Task.Delay(50);
                }
                _ready.TrySetException(new TimeoutException("Application did not become ready."));
            };
            core.Navigate($"https://content.local/sample.html?app={_caseId}");
            _parentWatch.Start();
            _ = ListenAsync();
        }
        catch (Exception e)
        {
            if (!_lifetime.IsCancellationRequested)
                Content = new System.Windows.Controls.TextBlock { Text = e.Message, Margin = new Thickness(30) };
        }
    }

    private async Task ListenAsync()
    {
        var token = _lifetime.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token);
                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(token);
                if (line is null) continue;
                await _ready.Task.WaitAsync(token);
                var request = JsonSerializer.Deserialize<JsonElement>(line, DemoSession.Json);
                var task = await Dispatcher.InvokeAsync(() => ExecuteAsync(request));
                var response = await task;
                await writer.WriteLineAsync(response.AsMemory(), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (IOException) { /* Client cancellation/disconnection; accept the next connection. */ }
            catch (Exception) when (!token.IsCancellationRequested) { await Task.Delay(100, token); }
        }
    }

    private Task<string> ExecuteAsync(JsonElement request)
    {
        object? viewportBounds = null;
        if (request.TryGetProperty("bounds", out var bounds))
        {
            var point = _view.PointFromScreen(new Point(bounds.GetProperty("x").GetDouble(), bounds.GetProperty("y").GetDouble()));
            var dpi = VisualTreeHelper.GetDpi(_view);
            viewportBounds = new { x = point.X, y = point.Y, width = bounds.GetProperty("width").GetDouble() / dpi.DpiScaleX, height = bounds.GetProperty("height").GetDouble() / dpi.DpiScaleY };
        }
        var json = JsonSerializer.Serialize(new { request, viewportBounds }, DemoSession.Json);
        return _view.CoreWebView2.ExecuteScriptAsync($"window.sampleCommand({json})");
    }

    private static bool IsLocal(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && parsed.Scheme == "https" && parsed.Host == "content.local" && parsed.IsDefaultPort;

    private static ImageSource CreateIcon(string caseId)
    {
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            var brush = new SolidColorBrush(caseId switch { "game" => Color.FromRgb(38, 75, 85), "letters" => Color.FromRgb(57, 100, 167), "comic" => Color.FromRgb(133, 88, 100), _ => Color.FromRgb(94, 111, 76) });
            dc.DrawRoundedRectangle(brush, null, new Rect(0, 0, 32, 32), 7, 7);
            dc.DrawLine(new Pen(Brushes.White, 3), new Point(10, 8), new Point(10, 24));
            dc.DrawLine(new Pen(Brushes.White, 3), new Point(10, 24), new Point(24, 24));
            dc.DrawEllipse(Brushes.White, null, new Point(22, 11), 4, 4);
        }
        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing); bitmap.Freeze(); return bitmap;
    }
}
