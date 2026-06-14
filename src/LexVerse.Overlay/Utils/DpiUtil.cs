using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LexVerse.Overlay.Utils
{
    internal static class DpiUtil
    {
        private const int MDT_EFFECTIVE_DPI = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        public static double GetScaleForMonitor(string? screenId = null)
        {
            try
            {
                Screen? target = null;
                if (!string.IsNullOrEmpty(screenId))
                {
                    foreach (var s in Screen.AllScreens)
                    {
                        if (string.Equals(s.DeviceName, screenId, StringComparison.OrdinalIgnoreCase)) { target = s; break; }
                    }
                }

                if (target == null) target = Screen.PrimaryScreen;

                var bounds = target.Bounds;
                var pt = new System.Drawing.Point(bounds.X + 1, bounds.Y + 1);
                var hmon = MonitorFromPoint(pt, 2); // MONITOR_DEFAULTTONEAREST
                if (hmon == IntPtr.Zero) return 1.0;

                if (GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    return dpiX / 96.0;
                }
            }
            catch { }
            return 1.0;
        }

        public static double PhysicalToDip(double physicalPixels, double dpiScale)
        {
            if (dpiScale <= 0) dpiScale = 1.0;
            return physicalPixels / dpiScale;
        }
    }
}
