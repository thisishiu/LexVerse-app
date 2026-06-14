using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using LexVerse.Overlay.Models;
using LexVerse.Overlay.Utils;

namespace LexVerse.Overlay.Services
{
    public class OverlayController : IOverlayService, IDisposable
    {
        public event EventHandler<OverlayStatus>? StatusChanged;
        private Thread? _uiThread;
        private LexVerse.Overlay.MainWindow? _window;
        private readonly object _sync = new object();

        public OverlayStatus Status { get; private set; } = OverlayStatus.Stopped;

        public void Start()
        {
            lock (_sync)
            {
                if (Status == OverlayStatus.Running || Status == OverlayStatus.Starting) return;
                SetStatus(OverlayStatus.Starting);

                _uiThread = new Thread(() =>
                {
                    try
                    {
                        var app = new LexVerse.Overlay.App();
                        _window = new LexVerse.Overlay.MainWindow();
                        SetStatus(OverlayStatus.Running);
                        app.Run(_window);
                    }
                    catch (Exception ex)
                    {
                        SetStatus(OverlayStatus.Error);
                        Logger.Log("Overlay thread exception: " + ex);
                    }
                });

                _uiThread.SetApartmentState(ApartmentState.STA);
                _uiThread.IsBackground = false;
                _uiThread.Start();
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (Status == OverlayStatus.Stopped) return;
                if (_window != null)
                {
                    _window.Dispatcher.Invoke(() => _window.Close());
                    _window = null;
                }

                SetStatus(OverlayStatus.Stopped);
            }
        }

        public void Show()
        {
            if (_window == null) return;
            _window.Dispatcher.Invoke(() => _window.Show());
            SetStatus(OverlayStatus.Running);
        }

        public void Hide()
        {
            if (_window == null) return;
            _window.Dispatcher.Invoke(() => _window.Hide());
            SetStatus(OverlayStatus.Hidden);
        }

        public void UpdateItems(IEnumerable<OverlayTextItem> items)
        {
            if (_window == null) return;

            // Map OverlayTextItem (physical pixels) -> TextItem (WPF DIP)
            var list = items.Select(i => new Models.TextItem
            {
                Text = i.Text,
                FontSize = i.FontSize,
                X = DpiUtil.PhysicalToDip(i.X, DpiUtil.GetScaleForMonitor(i.ScreenId)),
                Y = DpiUtil.PhysicalToDip(i.Y, DpiUtil.GetScaleForMonitor(i.ScreenId)),
                MaxWidth = i.MaxWidth,
                Wrap = i.Wrap,
                Background = i.Background ?? "#80000000",
                Foreground = i.Foreground ?? "White",
            }).ToList();

            _window.Dispatcher.Invoke(() =>
            {
                if (_window.DataContext is ViewModels.OverlayViewModel vm)
                {
                    vm.Items.Clear();
                    foreach (var t in list)
                        vm.Items.Add(t);
                }
            });
        }

        public void Clear()
        {
            if (_window == null) return;
            _window.Dispatcher.Invoke(() =>
            {
                if (_window.DataContext is ViewModels.OverlayViewModel vm)
                    vm.Items.Clear();
            });
        }

        public void ToggleClickThrough(bool enable)
        {
            if (_window == null) return;
            _window.Dispatcher.Invoke(() =>
            {
                // Set or clear WS_EX_TRANSPARENT on window
                var hwnd = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
                const int GWL_EXSTYLE = -20;
                const int WS_EX_TRANSPARENT = 0x20;
                const int WS_EX_LAYERED = 0x80000;
                const int WS_EX_TOOLWINDOW = 0x80;
                const int WS_EX_NOACTIVATE = 0x08000000;

                int exStyle = NativeMethods.GetWindowLongPtr32(hwnd, GWL_EXSTYLE);
                if (enable) exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                else exStyle &= ~WS_EX_TRANSPARENT;
                NativeMethods.SetWindowLongPtr32(hwnd, GWL_EXSTYLE, exStyle);
            });
        }

        private void SetStatus(OverlayStatus s)
        {
            Status = s;
            try { StatusChanged?.Invoke(this, s); } catch { }
            Logger.Log("Overlay status: " + s.ToString());
        }

        public void Dispose()
        {
            Stop();
        }
    }

    internal static class NativeMethods
    {
        // Use safe wrappers; on 64-bit GetWindowLongPtr should be used but this helper maps to correct API.
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        public static int GetWindowLongPtr32(IntPtr hWnd, int nIndex) => GetWindowLong32(hWnd, nIndex);
        public static int SetWindowLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong) => SetWindowLong32(hWnd, nIndex, dwNewLong);
    }
}
