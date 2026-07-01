using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LexVerse.Core.Models;

namespace LexVerse.App;

public partial class TranslationPopupWindow : Window
{
    private const int VkLeftButton = 0x01;
    private Dictionary<string, TermAnnotation> _annotationsByTerm = new(StringComparer.OrdinalIgnoreCase);
    private readonly SourceHighlightOverlayWindow _sourceOverlay;
    private readonly DispatcherTimer _outsideClickTimer;
    private readonly ScreenSelection _selection;
    private bool _wasLeftButtonPressed;
    private bool _wasUserPositioned;

    public TranslationPopupWindow(TranslationResult result, ScreenSelection selection)
    {
        InitializeComponent();
        _selection = selection;
        _sourceOverlay = new SourceHighlightOverlayWindow(selection);
        _outsideClickTimer = CreateOutsideClickTimer();
        PositionNear(selection);
        ShowResult(result);
    }

    private TranslationPopupWindow(ScreenSelection selection)
    {
        InitializeComponent();
        _selection = selection;
        _sourceOverlay = new SourceHighlightOverlayWindow(selection);
        _outsideClickTimer = CreateOutsideClickTimer();
        PositionNear(selection);
        SetResultVisibility(false);
    }

    public static TranslationPopupWindow CreateLoading(ScreenSelection selection)
    {
        return new TranslationPopupWindow(selection);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    public void ShowResult(TranslationResult result)
    {
        _annotationsByTerm = result.Annotations
            .Where(annotation => !string.IsNullOrWhiteSpace(annotation.Term))
            .GroupBy(annotation => annotation.Term, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        LoadingPanel.Visibility = Visibility.Collapsed;
        SetResultVisibility(true);
        SourceLabel.Visibility = Visibility.Collapsed;
        SourceSection.Visibility = Visibility.Collapsed;
        TranslatedLabel.Text = "Vietnamese";
        SourceTextBlock.Text = result.SourceText;
        BuildTranslatedSegments(result);
        KeywordDetailPanel.Visibility = Visibility.Collapsed;
        _sourceOverlay.Show();
        RepositionAfterLayout();
    }

    public void ShowError(string message)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        SetResultVisibility(true);
        SourceLabel.Text = "Lexverse could not translate this area";
        TranslatedLabel.Text = "Provider message";
        SourceTextBlock.Text = "The selected area was captured, but translation did not finish.";
        TranslatedItemsPanel.Children.Clear();
        TranslatedItemsPanel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 219, 194)),
            FontSize = 17,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 25
        });
        KeywordDetailPanel.Visibility = Visibility.Collapsed;
        RepositionAfterLayout();
    }

    private void SetResultVisibility(bool isVisible)
    {
        var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        SourceLabel.Visibility = visibility;
        SourceSection.Visibility = visibility;
        TranslatedLabel.Visibility = visibility;
        TranslatedSection.Visibility = visibility;
    }

    private void BuildTranslatedSegments(TranslationResult result)
    {
        TranslatedItemsPanel.Children.Clear();
        var segments = result.Segments is { Count: > 0 }
            ? result.Segments
            : [new TranslationSegment(0, result.SourceText, result.TranslatedText, null)];

        foreach (var segment in segments)
        {
            var item = new Border
            {
                Padding = new Thickness(7, 5, 7, 5),
                Margin = new Thickness(0, TranslatedItemsPanel.Children.Count == 0 ? 0 : 4, 0, 0),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = segment.SourceBounds
            };

            var textBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(247, 241, 231)),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22
            };

            BuildTranslatedTextRuns(textBlock, segment.TranslatedText);
            item.Child = textBlock;
            item.MouseEnter += Segment_MouseEnter;
            item.MouseLeave += Segment_MouseLeave;
            TranslatedItemsPanel.Children.Add(item);
        }
    }

    private void BuildTranslatedTextRuns(TextBlock textBlock, string translatedText)
    {
        var ranges = FindKeywordRanges(translatedText);
        var cursor = 0;

        foreach (var range in ranges)
        {
            if (range.Start > cursor)
            {
                textBlock.Inlines.Add(new Run(translatedText[cursor..range.Start]));
            }

            var run = new Run(translatedText.Substring(range.Start, range.Length))
            {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 218, 166)),
                Background = new SolidColorBrush(Color.FromArgb(88, 168, 111, 63)),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                TextDecorations = TextDecorations.Underline,
                Tag = range.Detail
            };

            run.MouseEnter += Keyword_MouseEnter;
            run.MouseLeave += Keyword_MouseLeave;
            textBlock.Inlines.Add(run);
            cursor = range.Start + range.Length;
        }

        if (cursor < translatedText.Length)
        {
            textBlock.Inlines.Add(new Run(translatedText[cursor..]));
        }

        if (textBlock.Inlines.Count == 0)
        {
            textBlock.Inlines.Add(new Run(translatedText));
        }
    }

    private IReadOnlyList<KeywordRange> FindKeywordRanges(string translatedText)
    {
        var candidates = new List<KeywordCandidate>();

        foreach (var annotation in _annotationsByTerm.Values)
        {
            candidates.Add(new KeywordCandidate(
                DisplayText: annotation.Term,
                SearchText: annotation.Term,
                Detail: new KeywordDetail(
                    Title: annotation.Term,
                    Body: $"{annotation.Meaning}. {annotation.Description}".Trim())));
        }

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.SearchText))
            .OrderByDescending(candidate => candidate.SearchText.Length)
            .Select(candidate => FindRange(translatedText, candidate))
            .Where(range => range is not null)
            .Select(range => range!)
            .OrderBy(range => range.Start)
            .Aggregate(new List<KeywordRange>(), AddNonOverlappingRange);
    }

    private static List<KeywordRange> AddNonOverlappingRange(List<KeywordRange> ranges, KeywordRange candidate)
    {
        var overlaps = ranges.Any(existing =>
            candidate.Start < existing.Start + existing.Length
            && existing.Start < candidate.Start + candidate.Length);

        if (!overlaps)
        {
            ranges.Add(candidate);
        }

        return ranges;
    }

    private static KeywordRange? FindRange(string text, KeywordCandidate candidate)
    {
        var index = text.IndexOf(candidate.SearchText, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? null
            : new KeywordRange(index, candidate.SearchText.Length, candidate.Detail);
    }

    private void PositionNear(ScreenSelection selection)
    {
        Left = Math.Max(SystemParameters.VirtualScreenLeft + 12, selection.X);
        Top = Math.Max(SystemParameters.VirtualScreenTop + 12, selection.Y - 240);
    }

    private void Keyword_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Run { Tag: KeywordDetail detail } run)
        {
            return;
        }

        run.Background = new SolidColorBrush(Color.FromArgb(135, 168, 111, 63));
        KeywordTitle.Text = detail.Title;
        KeywordBody.Text = detail.Body;
        KeywordDetailPanel.Visibility = Visibility.Visible;
    }

    private void Keyword_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Run run)
        {
            run.Background = new SolidColorBrush(Color.FromArgb(88, 168, 111, 63));
        }

        KeywordDetailPanel.Visibility = Visibility.Collapsed;
    }

    private void Segment_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.Background = new SolidColorBrush(Color.FromArgb(38, 198, 155, 103));
        if (border.Tag is SourceBounds bounds)
        {
            _sourceOverlay.ShowHighlight(bounds);
        }
        else
        {
            _sourceOverlay.ShowWholeSelectionHint();
        }
    }

    private void Segment_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Brushes.Transparent;
        }

        _sourceOverlay.ClearHighlight();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _wasUserPositioned = true;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _outsideClickTimer.Stop();
        _sourceOverlay.Close();
        base.OnClosed(e);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RepositionAfterLayout();
        _outsideClickTimer.Start();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)));
        if (PopupShell.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(190))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }
    }

    private DispatcherTimer CreateOutsideClickTimer()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };

        timer.Tick += (_, _) => CloseIfClickedOutside();
        return timer;
    }

    private void CloseIfClickedOutside()
    {
        var isLeftButtonPressed = (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
        if (!isLeftButtonPressed)
        {
            _wasLeftButtonPressed = false;
            return;
        }

        if (_wasLeftButtonPressed)
        {
            return;
        }

        _wasLeftButtonPressed = true;
        if (!IsMouseInsidePopup())
        {
            Close();
        }
    }

    private bool IsMouseInsidePopup()
    {
        if (!GetCursorPos(out var point))
        {
            return true;
        }

        return point.X >= Left
            && point.Y >= Top
            && point.X <= Left + ActualWidth
            && point.Y <= Top + ActualHeight;
    }

    private void PositionOutsideSelection()
    {
        if (_wasUserPositioned)
        {
            return;
        }

        var margin = 14;
        var screenLeft = SystemParameters.VirtualScreenLeft + margin;
        var screenTop = SystemParameters.VirtualScreenTop + margin;
        var screenRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - margin;
        var screenBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - margin;

        var width = Math.Max(ActualWidth, 420);
        var height = Math.Max(ActualHeight, 160);

        var aboveTop = _selection.Y - height - margin;
        var belowTop = _selection.Y + _selection.Height + margin;
        var rightLeft = _selection.X + _selection.Width + margin;
        var leftLeft = _selection.X - width - margin;

        if (aboveTop >= screenTop)
        {
            Left = Clamp(_selection.X, screenLeft, screenRight - width);
            Top = aboveTop;
        }
        else if (belowTop + height <= screenBottom)
        {
            Left = Clamp(_selection.X, screenLeft, screenRight - width);
            Top = belowTop;
        }
        else if (rightLeft + width <= screenRight)
        {
            Left = rightLeft;
            Top = Clamp(_selection.Y, screenTop, screenBottom - height);
        }
        else if (leftLeft >= screenLeft)
        {
            Left = leftLeft;
            Top = Clamp(_selection.Y, screenTop, screenBottom - height);
        }
        else
        {
            Left = Clamp(_selection.X, screenLeft, screenRight - width);
            Top = Clamp(belowTop, screenTop, screenBottom - height);
        }
    }

    private void RepositionAfterLayout()
    {
        Dispatcher.BeginInvoke(PositionOutsideSelection, DispatcherPriority.Render);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Max(min, Math.Min(max, value));
    }

    private sealed record KeywordCandidate(string DisplayText, string SearchText, KeywordDetail Detail);
    private sealed record KeywordDetail(string Title, string Body);
    private sealed record KeywordRange(int Start, int Length, KeywordDetail Detail);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
