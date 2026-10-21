using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace LexVerse.Overlay;

// FormattedText wraps long words with overflow; the overlay's TextBlock uses Wrap.
// Use the same formatter with explicit Wrap so unbroken translations fit as well.
internal sealed class OverlayTextMeasurer : IDisposable
{
    private readonly TextFormatter _formatter = TextFormatter.Create(TextFormattingMode.Ideal);

    internal bool Fits(string text, double fontSize, double width, double height, double pixelsPerDip)
    {
        var properties = new RunProperties(fontSize);
        var source = new Source(text, properties) { PixelsPerDip = pixelsPerDip };
        var paragraph = new Paragraph(properties);
        var position = 0;
        double totalHeight = 0;
        TextLineBreak? previousBreak = null;
        try
        {
            do
            {
                using var line = _formatter.FormatLine(source, position, width, paragraph, previousBreak);
                previousBreak?.Dispose();
                previousBreak = null;
                totalHeight += line.Height;
                // TextBlock does not require trailing wrap whitespace to fit.
                if (line.Width > width || totalHeight > height || line.Length == 0)
                    return false;
                position += line.Length;
                previousBreak = line.GetTextLineBreak();
                // A formatter line may consume exactly the remaining characters.
                // Only measure another line at the end for an explicit line break,
                // as TextBlock does for a trailing newline.
            } while (position < text.Length ||
                (position == text.Length && text[^1] is '\r' or '\n' or '\u2028' or '\u2029'));
            return true;
        }
        finally { previousBreak?.Dispose(); }
    }

    public void Dispose() => _formatter.Dispose();

    private sealed class Source(string text, TextRunProperties properties) : TextSource
    {
        public override TextRun GetTextRun(int index) => index >= text.Length
            ? new TextEndOfParagraph(1)
            : new TextCharacters(text, index, text.Length - index, properties);
        public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int limit) =>
            new(limit, new CultureSpecificCharacterBufferRange(CultureInfo.CurrentUICulture,
                new CharacterBufferRange(text, 0, limit)));
        public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int index) => index;
    }

    private sealed class RunProperties(double size) : TextRunProperties
    {
        public override Typeface Typeface { get; } = new(new FontFamily("Segoe UI"), FontStyles.Normal,
            FontWeights.SemiBold, FontStretches.Normal);
        public override double FontRenderingEmSize => size;
        public override double FontHintingEmSize => size;
        public override TextDecorationCollection? TextDecorations => null;
        public override Brush ForegroundBrush => Brushes.Black;
        public override Brush? BackgroundBrush => null;
        public override CultureInfo CultureInfo => CultureInfo.CurrentUICulture;
        public override TextEffectCollection? TextEffects => null;
    }

    private sealed class Paragraph(TextRunProperties properties) : TextParagraphProperties
    {
        public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
        public override TextAlignment TextAlignment => TextAlignment.Left;
        public override double LineHeight => 0;
        public override bool FirstLineInParagraph => true;
        public override TextRunProperties DefaultTextRunProperties => properties;
        public override TextWrapping TextWrapping => TextWrapping.Wrap;
        public override TextMarkerProperties? TextMarkerProperties => null;
        public override double Indent => 0;
    }
}
