using LexVerse.Core.Geometry;
using LexVerse.Overlay;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class PhysicalPixelToDipConverterTests
{
    [Fact]
    public void ToLocalDip_OnOffset125PercentMonitor_UsesCaptureMonitorScale()
    {
        var converter = new PhysicalPixelToDipConverter(
            new ScreenPoint(-2560, 444),
            1.25,
            1.25);

        var local = converter.ToLocalDip(new ScreenRect(-2500, 744, 500, 50));

        Assert.Equal(48, local.X);
        Assert.Equal(240, local.Y);
        Assert.Equal(400, local.Width);
        Assert.Equal(40, local.Height);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1.25, 320, 180)]
    [InlineData(1.5, -2560, 444)]
    [InlineData(2, -1920, -1080)]
    public void FrameToLocalDip_WithCaptureOffsetAndDpi_PreservesOcrRectangle(
        double dpi, double originX, double originY)
    {
        var geometry = new FrameGeometry(
            new PixelSize(800, 600),
            new ScreenRect(originX, originY, 1600, 1200),
            CoordinateSpace.FrameLocal, dpi, dpi, 1);
        var mapper = new CoordinateMapper(geometry);
        var converter = new PhysicalPixelToDipConverter(new ScreenPoint(originX, originY), dpi, dpi);

        var screen = mapper.FrameToScreen(new FrameRect(30, 50, 120, 20));
        var local = converter.ToLocalDip(screen);

        Assert.Equal(60 / dpi, local.X, 8);
        Assert.Equal(100 / dpi, local.Y, 8);
        Assert.Equal(240 / dpi, local.Width, 8);
        Assert.Equal(40 / dpi, local.Height, 8);
        var roundTrip = mapper.ScreenToFrame(screen);
        Assert.Equal(new FrameRect(30, 50, 120, 20), roundTrip);
    }

    [Theory]
    [InlineData(0, 1, "dpiScaleX")]
    [InlineData(1, 0, "dpiScaleY")]
    public void Constructor_WhenScaleIsInvalid_Throws(
        double dpiScaleX,
        double dpiScaleY,
        string parameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PhysicalPixelToDipConverter(new ScreenPoint(0, 0), dpiScaleX, dpiScaleY));

        Assert.Equal(parameterName, exception.ParamName);
    }
}
