using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OverlayWindowLayerTests
{
    [Theory]
    [InlineData(0, 123, 123)]
    [InlineData(0, 0, -1)]
    [InlineData(456, 123, -2)]
    [InlineData(456, 0, -2)]
    public void BoundsPlacement_UsesPriorityOnlyForScreenOverlay(int target, int priority, int expected)
    {
        Assert.Equal(new IntPtr(expected), LexVerse.Overlay.MainWindow.GetBoundsInsertAfter(
            new IntPtr(target), new IntPtr(priority)));
    }

    [Fact]
    public void BoundsPlacement_WhenPriorityEnds_ReturnsToTopmost()
    {
        Assert.Equal(new IntPtr(123), LexVerse.Overlay.MainWindow.GetBoundsInsertAfter(
            IntPtr.Zero, new IntPtr(123)));
        Assert.Equal(new IntPtr(-1), LexVerse.Overlay.MainWindow.GetBoundsInsertAfter(
            IntPtr.Zero, IntPtr.Zero));
    }
}
