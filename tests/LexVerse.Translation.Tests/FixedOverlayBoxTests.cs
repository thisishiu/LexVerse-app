using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LexVerse.Overlay;
using LexVerse.Overlay.Models;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class FixedOverlayBoxTests
{
    [Fact]
    public void RenderBeforeAndAfterSourceHeightCorrection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                const string text = "Tiếng Việt có dấu Ắ ễ ộ ự g";
                var items = new ObservableCollection<TextItem>();
                var source = new LexVerse.Core.Geometry.ScreenRect(8, 8, 300, 50);
                var mask = OverlaySourceGeometry.GetMaskBounds(source);
                var preferred = OverlaySourceGeometry.FontSizeFromInkHeight(20);
                foreach (var after in new[] { false, true })
                {
                    var rect = after ? mask : source;
                    var layout = OverlayTextLayoutCalculator.Calculate(text, rect.Width, rect.Height,
                        rect.Width, after ? preferred : 20, 8, 4, 2, 1);
                    items.Add(new TextItem { Text = text, X = rect.X, Y = rect.Y + (after ? 70 : 0),
                        Width = layout.Width, Height = layout.Height, FontSize = layout.FontSize });
                }
                Assert.True(items[1].FontSize > items[0].FontSize);
                var window = new LexVerse.Overlay.MainWindow(items);
                try
                {
                    var root = (FrameworkElement)window.Content;
                    root.Measure(new Size(330, 140));
                    root.Arrange(new Rect(0, 0, 330, 140));
                    root.UpdateLayout();
                    foreach (var border in Descendants(root).OfType<Border>().Where(b => b.DataContext is TextItem))
                    {
                        var block = Assert.IsType<TextBlock>(border.Child);
                        var width = border.ActualWidth - border.Padding.Left - border.Padding.Right
                            - border.BorderThickness.Left - border.BorderThickness.Right;
                        var height = border.ActualHeight - border.Padding.Top - border.Padding.Bottom
                            - border.BorderThickness.Top - border.BorderThickness.Bottom;
                        block.Measure(new Size(width, double.PositiveInfinity));
                        Assert.True(block.DesiredSize.Height <= height + 0.01);
                        Assert.True(block.DesiredSize.Width <= width + 0.01);
                    }
                    var bitmap = new RenderTargetBitmap(330, 140, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        "LexVerse-overlay-before-after.png"));
                    encoder.Save(output);
                }
                finally { window.Close(); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF render timed out");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void KeepsPreferredFontWhenItFitsAndOnEmptyText()
    {
        Assert.Equal(20, Calculate("Short", 200, 50).FontSize);
        Assert.Equal(20, Calculate("", 1, 1).FontSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void OnlyShrinksEnoughToFitBothDimensions(double dpi)
    {
        const string text = "Tiếng Việt có dấu Ắ ễ ộ ự g\nBản dịch nhiều dòng";
        var layout = Calculate(text, 130, 25, dpi);
        Assert.True(layout.FontSize < 20);
        Assert.True(Fits(text, layout.FontSize, 120, 19, dpi));
        Assert.False(Fits(text, layout.FontSize + 0.011, 120, 19, dpi));
    }

    [Fact]
    public void ShrinksBelowFourWithoutChangingBox()
    {
        var layout = Calculate(string.Join('\n', Enumerable.Repeat("Bản dịch rất dài", 20)), 60, 12);
        Assert.InRange(layout.FontSize, 0.01, 3.99);
        Assert.Equal(60, layout.Width);
        Assert.Equal(12, layout.Height);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(20, 0)]
    [InlineData(0.001, 0.001)]
    public void DegenerateRegionHasSafeFontAndDoesNotExpand(double width, double height)
    {
        var layout = Calculate("Text", width, height);
        Assert.Equal(width, layout.Width);
        Assert.Equal(height, layout.Height);
        Assert.InRange(layout.FontSize, 0.01, 20);
    }

    [Fact]
    public void PartialUpdatesStartFromPreferredFont()
    {
        var first = Calculate("Short", 200, 50);
        Calculate(string.Join(' ', Enumerable.Repeat("Long translation", 50)), 200, 50);
        Assert.Equal(first, Calculate("Short", 200, 50));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void ActualTemplateContainsTextAndPreservesAdjacentBoxBounds(double dpi)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var samples = new (string Text, double Width, double Height)[]
                {
                    ("Short", 200, 50), ("Tiếng Việt có dấu Ắ ễ ộ ự g", 180, 20),
                    ("First line\nDòng thứ hai\nDòng thứ ba", 130, 25),
                    (new string('W', 70), 100, 20),
                    (string.Join('\n', Enumerable.Repeat("Bản dịch rất dài", 20)), 60, 12),
                    ("Chữ trong box nhỏ", 6, 5)
                };
                var items = new ObservableCollection<TextItem>();
                double y = 0;
                foreach (var sample in samples)
                {
                    var layout = Calculate(sample.Text, sample.Width, sample.Height, dpi);
                    items.Add(new TextItem { Text = sample.Text, X = 5, Y = y,
                        Width = layout.Width, Height = layout.Height, MinHeight = layout.Height, FontSize = layout.FontSize });
                    y += sample.Height;
                }
                var window = new LexVerse.Overlay.MainWindow(items);
                try
                {
                    var root = (FrameworkElement)window.Content;
                    root.Measure(new Size(400, 250));
                    root.Arrange(new Rect(0, 0, 400, 250));
                    root.UpdateLayout();
                    var borders = Descendants(root).OfType<Border>().Where(b => b.DataContext is TextItem).ToArray();
                    Assert.Equal(samples.Length, borders.Length);
                    foreach (var border in borders)
                    {
                        var item = (TextItem)border.DataContext;
                        if (item.Text.StartsWith("WW", StringComparison.Ordinal))
                            Assert.True(item.FontSize > 2, "A long word should wrap instead of being forced onto one tiny line.");
                        if (item.Width == 6)
                        {
                            Assert.True(border.Padding.Left < 4);
                            Assert.True(border.Padding.Top < 2);
                        }
                        Assert.Equal(item.Width, border.ActualWidth, 6);
                        Assert.Equal(item.Height, border.ActualHeight, 6);
                        var position = border.TransformToAncestor(root).Transform(new Point());
                        Assert.Equal(item.X, position.X, 6);
                        Assert.Equal(item.Y, position.Y, 6);
                        var text = Assert.IsType<TextBlock>(border.Child);
                        Assert.Equal(TextTrimming.None, text.TextTrimming);
                        var availableWidth = border.ActualWidth - border.Padding.Left - border.Padding.Right
                            - border.BorderThickness.Left - border.BorderThickness.Right;
                        var availableHeight = border.ActualHeight - border.Padding.Top - border.Padding.Bottom
                            - border.BorderThickness.Top - border.BorderThickness.Bottom;
                        text.Measure(new Size(availableWidth, double.PositiveInfinity));
                        Assert.True(text.DesiredSize.Height <= availableHeight + 0.01,
                            $"'{item.Text}': font {item.FontSize}, needs {text.DesiredSize.Height}, has {availableHeight}");
                        Assert.True(text.DesiredSize.Width <= availableWidth + 0.01);
                    }
                    root.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)(400 * dpi), (int)(250 * dpi), 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                        $"LexVerse-fixed-box-{dpi.ToString(CultureInfo.InvariantCulture)}.png"));
                    encoder.Save(output);
                }
                finally { window.Close(); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF render timed out");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static OverlayTextLayout Calculate(string text, double width, double height, double dpi = 1) =>
        OverlayTextLayoutCalculator.Calculate(text, width, height, width, 20, 8, 4, 2, dpi);

    private static bool Fits(string text, double font, double width, double height, double dpi)
    {
        var measured = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            font, Brushes.Black, dpi) { MaxTextWidth = width, Trimming = TextTrimming.None };
        return measured.WidthIncludingTrailingWhitespace <= width && measured.Height <= height;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
