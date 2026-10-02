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
