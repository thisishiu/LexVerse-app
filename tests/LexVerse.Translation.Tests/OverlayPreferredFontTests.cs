using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using LexVerse.Overlay;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class OverlayPreferredFontTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void RetainsPreferredSizeWheneverActualTextBlockFits(double dpi)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var size in new[] { 20d, 60d })
                foreach (var text in new[] { "Short", "Tiếng Việt có dấu Ắ ễ ộ ự g",
                    "First line\nDòng thứ hai", "Trailing newline\n", "Trailing CRLF\r\n",
                    "Blank line\n\nNext", "Wrap these words onto multiple lines inside the box",
                    new string('W', 70) })
                {
                    const double width = 300;
                    var block = new TextBlock
                    {
                        Text = text,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = size,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.None,
                        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag)
                    };
                    VisualTreeHelper.SetRootDpi(block, new DpiScale(dpi, dpi));
                    block.Measure(new Size(width - 10, double.PositiveInfinity));
                    var height = block.DesiredSize.Height + 6 + 0.001;
                    var layout = OverlayTextLayoutCalculator.Calculate(text, width, height,
                        width, size, 8, 4, 2, dpi);
                    Assert.True(layout.FontSize == size,
                        $"'{text}' at DPI {dpi}: expected {size}, got {layout.FontSize}");
                    Assert.Equal(width, layout.Width);
                    Assert.Equal(height, layout.Height);

                    // Removing a full line of space must force fitting, including
                    // when that line was explicitly left blank by the translation.
                    var smaller = OverlayTextLayoutCalculator.Calculate(text, width, height / 2,
                        width, size, 8, 4, 2, dpi);
                    Assert.True(smaller.FontSize < size);
                    block.FontSize = smaller.FontSize;
                    block.Measure(new Size(width - 10, double.PositiveInfinity));
                    Assert.True(block.DesiredSize.Height <= height / 2 - 6 + 0.01);
                }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF measurement timed out");
        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
