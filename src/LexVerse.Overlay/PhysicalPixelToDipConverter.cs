using System.Windows;
using System.Windows.Media;
using LexVerse.Core.Geometry;

namespace LexVerse.Overlay;

public sealed class PhysicalPixelToDipConverter
{
    private readonly ScreenPoint _origin;
    private readonly double _dpiScaleX;
    private readonly double _dpiScaleY;

    public PhysicalPixelToDipConverter(Visual visual, ScreenPoint origin)
        : this(origin, GetDpi(visual).DpiScaleX, GetDpi(visual).DpiScaleY)
    {
    }

    public PhysicalPixelToDipConverter(ScreenPoint origin, double dpiScaleX, double dpiScaleY)
    {
        if (dpiScaleX <= 0 || dpiScaleY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiScaleX), "DPI scale must be greater than zero.");
        }

        _origin = origin;
        _dpiScaleX = dpiScaleX;
        _dpiScaleY = dpiScaleY;
    }

    public Rect ToLocalDip(ScreenRect physicalRect)
    {
        return new Rect(
            (physicalRect.X - _origin.X) / _dpiScaleX,
            (physicalRect.Y - _origin.Y) / _dpiScaleY,
            physicalRect.Width / _dpiScaleX,
            physicalRect.Height / _dpiScaleY);
    }

    private static DpiScale GetDpi(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        return VisualTreeHelper.GetDpi(visual);
    }
}
