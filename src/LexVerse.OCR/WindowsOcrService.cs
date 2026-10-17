using LexVerse.Core.Imaging;
using LexVerse.Core.Ocr;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Security.Cryptography;
using CoreOcrWord = LexVerse.Core.Ocr.OcrWord;

namespace LexVerse.OCR;

public sealed class WindowsOcrService : IOcrService
{
    private const string DefaultLanguageTag = "en-US";
    private const double MinimumHorizontalSplitGap = 22;
    private static readonly string[] LatinGuardLanguageTags = ["vi-VN", DefaultLanguageTag];

    private readonly OcrEngine _engine;
    private readonly OcrEngine? _latinGuardEngine;
    private readonly OcrTextBlockGroupingMode _groupingMode;
    private readonly OcrTextBlockLayoutGrouper _layoutGrouper = new();
    private readonly ComicSpeechBubbleGrouper _comicSpeechBubbleGrouper = new();

    public WindowsOcrService(
        string? languageTag = null,
        OcrTextBlockGroupingMode groupingMode = OcrTextBlockGroupingMode.Layout)
    {
        _engine = CreateEngine(languageTag ?? DefaultLanguageTag);
        _latinGuardEngine = OcrTextLanguageFilter.IsJapaneseLanguage(
            _engine.RecognizerLanguage.LanguageTag)
            ? TryCreateLatinGuardEngine()
            : null;
        _groupingMode = groupingMode;
    }

    public async Task<LexVerse.Core.Ocr.OcrResult> RecognizeAsync(CapturedFrame frame, CancellationToken cancellationToken = default)
    {
        var debugResult = await RecognizeWithLayoutDebugAsync(frame, cancellationToken);
        return debugResult.Result;
    }

    public async Task<OcrLayoutDebugResult> RecognizeWithLayoutDebugAsync(CapturedFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.PixelFormat != PixelFormat.Bgra8)
        {
            throw new NotSupportedException($"Unsupported OCR pixel format: {frame.PixelFormat}.");
        }

        if (frame.Pixels.Length < frame.ExpectedByteCount)
        {
            throw new ArgumentException("Frame pixel buffer is smaller than width/height/stride metadata.", nameof(frame));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            CryptographicBuffer.CreateFromByteArray(frame.Pixels),
            BitmapPixelFormat.Bgra8,
            frame.Width,
            frame.Height,
            BitmapAlphaMode.Premultiplied);

        var result = await _engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();

        var recognizerLanguageTag = _engine.RecognizerLanguage.LanguageTag;
        IReadOnlyList<CoreOcrWord> filteredWords = result.Lines
            .SelectMany(line => line.Words)
            .Select(ToWord)
            .Select(word => OcrTextLanguageFilter.FilterRecognizedWord(word, recognizerLanguageTag))
            .OfType<CoreOcrWord>()
            .ToArray();

        if (_latinGuardEngine is not null && filteredWords.Count > 0)
        {
            var latinGuardResult = await _latinGuardEngine.RecognizeAsync(bitmap);
            cancellationToken.ThrowIfCancellationRequested();

            filteredWords = JapaneseLatinTextVeto.RemoveLikelyLatinHallucinations(
                BuildVisualTextBlocks(filteredWords),
                BuildNativeLineBlocks(latinGuardResult),
                result.TextAngle ?? 0,
                latinGuardResult.TextAngle ?? 0,
                frame.Width,
                frame.Height);
        }

        var recognizedWords = JapaneseRubyTextFilter.RemoveLikelyAnnotations(
            filteredWords,
            recognizerLanguageTag);

        var lineBlocks = BuildVisualTextBlocks(recognizedWords)
            .Where(block => OcrTextLanguageFilter.IsLikelyTextBlock(
                block,
                recognizerLanguageTag))
            .ToArray();
        var blocks = GroupLineBlocks(lineBlocks);

        var ocrResult = new LexVerse.Core.Ocr.OcrResult(
            blocks,
            _engine.RecognizerLanguage.LanguageTag,
            result.TextAngle ?? 0,
            DateTimeOffset.UtcNow);

        return new OcrLayoutDebugResult(lineBlocks, ocrResult);
    }

    private IReadOnlyList<OcrTextBlock> GroupLineBlocks(IReadOnlyList<OcrTextBlock> lineBlocks)
    {
        return _groupingMode switch
        {
            OcrTextBlockGroupingMode.ComicSpeechBubbles => _comicSpeechBubbleGrouper.GroupLines(lineBlocks),
            _ => _layoutGrouper.GroupLines(lineBlocks)
        };
    }

    private static OcrEngine CreateEngine(string? languageTag)
    {
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            var language = new Language(languageTag);
            if (OcrEngine.IsLanguageSupported(language))
            {
                return OcrEngine.TryCreateFromLanguage(language)
                    ?? throw new InvalidOperationException($"Could not create OCR engine for {languageTag}.");
            }
        }

        var defaultLanguage = new Language(DefaultLanguageTag);
        if (OcrEngine.IsLanguageSupported(defaultLanguage))
        {
            return OcrEngine.TryCreateFromLanguage(defaultLanguage)
                ?? throw new InvalidOperationException($"Could not create OCR engine for {DefaultLanguageTag}.");
        }

        throw new InvalidOperationException($"{DefaultLanguageTag} OCR is not installed or supported on this Windows profile.");
    }

    private static OcrEngine? TryCreateLatinGuardEngine()
    {
        foreach (var languageTag in LatinGuardLanguageTags)
        {
            try
            {
                var language = new Language(languageTag);
                if (!OcrEngine.IsLanguageSupported(language))
                {
                    continue;
                }

                var engine = OcrEngine.TryCreateFromLanguage(language);
                if (engine is not null)
                {
                    return engine;
                }
            }
            catch (ArgumentException)
            {
                // Try the next installed Latin OCR language.
            }
        }

        return null;
    }

    public static bool IsLanguageSupported(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return false;
        }

        try
        {
            return OcrEngine.IsLanguageSupported(new Language(languageTag));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static CoreOcrWord ToWord(Windows.Media.Ocr.OcrWord word)
    {
        var bounds = ToBoundingBox(word.BoundingRect);
        return new CoreOcrWord(word.Text, bounds, bounds.Height);
    }

    private static IReadOnlyList<OcrTextBlock> BuildNativeLineBlocks(Windows.Media.Ocr.OcrResult result)
    {
        return result.Lines
            .Select(line => line.Words.Select(ToWord).ToArray())
            .Where(words => words.Length > 0)
            .Select(words => ToTextBlock(words))
            .ToArray();
    }

    private static IReadOnlyList<OcrTextBlock> BuildVisualTextBlocks(IReadOnlyList<CoreOcrWord> words)
    {
        if (words.Count == 0)
        {
            return [];
        }

        var medianWordHeight = Median(words.Select(word => word.FontSize));
        var rowTolerance = Math.Max(5, medianWordHeight * 0.55);
        var rows = new List<VisualRow>();

        foreach (var word in words.OrderBy(word => CenterY(word.Bounds)).ThenBy(word => word.Bounds.X))
        {
            var matchingRow = rows
                .Where(row => Math.Abs(row.CenterY - CenterY(word.Bounds)) <= rowTolerance ||
                    VerticalOverlapRatio(row.Top, row.Bottom, word.Bounds.Y, word.Bounds.Bottom) >= 0.45)
                .OrderBy(row => Math.Abs(row.CenterY - CenterY(word.Bounds)))
                .FirstOrDefault();

            if (matchingRow is null)
            {
                rows.Add(new VisualRow(word));
                continue;
            }

            matchingRow.Add(word);
        }

        return rows
            .OrderBy(row => row.Top)
            .ThenBy(row => row.Left)
            .SelectMany(row => SplitRowIntoVisualLines(row.Words, medianWordHeight))
            .Select(ToTextBlock)
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<CoreOcrWord>> SplitRowIntoVisualLines(
        IReadOnlyList<CoreOcrWord> words,
        double medianWordHeight)
    {
        if (words.Count <= 1)
        {
            return [words];
        }

        var orderedWords = words
            .OrderBy(word => word.Bounds.X)
            .ThenBy(word => word.Bounds.Y)
            .ToArray();
        var gaps = orderedWords
            .Zip(orderedWords.Skip(1), (left, right) => right.Bounds.X - left.Bounds.Right)
            .Where(gap => gap > 0)
            .Select(gap => (double)gap)
            .ToArray();
        var columnGapThreshold = CalculateHorizontalSplitGap(gaps, medianWordHeight);
        var visualLines = new List<IReadOnlyList<CoreOcrWord>>();
        var currentLine = new List<CoreOcrWord> { orderedWords[0] };

        for (var index = 1; index < orderedWords.Length; index++)
        {
            var previousWord = orderedWords[index - 1];
            var currentWord = orderedWords[index];
            var gap = currentWord.Bounds.X - previousWord.Bounds.Right;

            if (gap > columnGapThreshold && IsMeaningfulHorizontalSplit(currentLine, orderedWords, index, gap, medianWordHeight))
            {
                visualLines.Add(currentLine.ToArray());
                currentLine.Clear();
            }

            currentLine.Add(currentWord);
        }

        if (currentLine.Count > 0)
        {
            visualLines.Add(currentLine.ToArray());
        }

        return visualLines;
    }

    private static bool IsMeaningfulHorizontalSplit(
        IReadOnlyList<CoreOcrWord> currentLine,
        IReadOnlyList<CoreOcrWord> orderedWords,
        int splitIndex,
        int gap,
        double medianWordHeight)
    {
        var leftWordCount = currentLine.Count;
        var rightWordCount = orderedWords.Count - splitIndex;
        if (leftWordCount == 0 || rightWordCount == 0)
        {
            return false;
        }

        if (Math.Min(leftWordCount, rightWordCount) >= 3)
        {
            return true;
        }

        return gap >= Math.Max(22, medianWordHeight * 1.8);
    }

    private static OcrTextBlock ToTextBlock(IReadOnlyList<CoreOcrWord> words)
    {
        var bounds = words.Count == 0
            ? new BoundingBox(0, 0, 0, 0)
            : words.Select(word => word.Bounds).Aggregate(BoundingBox.Union);
        var fontSize = words.Count == 0
            ? 0
            : Median(words.Select(word => word.FontSize));
        var text = string.Join(' ', words.Select(word => word.Text));

        return new OcrTextBlock(text, bounds, words, fontSize);
    }

    private static double CenterY(BoundingBox bounds)
    {
        return bounds.Y + bounds.Height / 2.0;
    }

    private static double VerticalOverlapRatio(int topA, int bottomA, int topB, int bottomB)
    {
        var overlap = Math.Min(bottomA, bottomB) - Math.Max(topA, topB);
        if (overlap <= 0)
        {
            return 0;
        }

        var smallerHeight = Math.Min(bottomA - topA, bottomB - topB);
        return smallerHeight <= 0
            ? 0
            : overlap / (double)smallerHeight;
    }

    private static BoundingBox ToBoundingBox(Windows.Foundation.Rect rect)
    {
        return new BoundingBox(
            (int)Math.Round(rect.X),
            (int)Math.Round(rect.Y),
            (int)Math.Round(rect.Width),
            (int)Math.Round(rect.Height));
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Where(value => value > 0).Order().ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }

    private static double CalculateHorizontalSplitGap(IReadOnlyList<double> gaps, double medianWordHeight)
    {
        var normalWordGaps = gaps
            .Where(gap => gap <= Math.Max(12, medianWordHeight * 1.2))
            .ToArray();
        var typicalWordGap = Median(normalWordGaps);

        return typicalWordGap > 0
            ? Math.Max(MinimumHorizontalSplitGap, Math.Max(medianWordHeight * 1.55, typicalWordGap * 2.8))
            : Math.Max(MinimumHorizontalSplitGap, medianWordHeight * 1.55);
    }

    private sealed class VisualRow
    {
        private readonly List<CoreOcrWord> _words = [];

        public VisualRow(CoreOcrWord firstWord)
        {
            Add(firstWord);
        }

        public int Left { get; private set; }

        public int Top { get; private set; }

        public int Bottom { get; private set; }

        public double CenterY => Top + (Bottom - Top) / 2.0;

        public IReadOnlyList<CoreOcrWord> Words => _words;

        public void Add(CoreOcrWord word)
        {
            if (_words.Count == 0)
            {
                Left = word.Bounds.X;
                Top = word.Bounds.Y;
                Bottom = word.Bounds.Bottom;
            }
            else
            {
                Left = Math.Min(Left, word.Bounds.X);
                Top = Math.Min(Top, word.Bounds.Y);
                Bottom = Math.Max(Bottom, word.Bounds.Bottom);
            }

            _words.Add(word);
        }
    }

}

internal static class OcrTextLanguageFilter
{
    // Japanese OCR can hallucinate one kana from stylized Latin logos.
    private const int MinimumJapaneseLetterCount = 2;
    private const double MinimumJapaneseLetterRatio = 0.30;
    private const string VietnameseDiacritics =
        "àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ" +
        "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬĐÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴ";

    public static bool IsLikelyTextBlock(OcrTextBlock block, string? languageTag)
    {
        if (IsJapaneseLanguage(languageTag))
        {
            return HasSufficientJapaneseText(block.Text);
        }

        return IsLikelyEnglish(block.Text) && !LooksLikeVisualNoise(block.Text);
    }

    public static CoreOcrWord? FilterRecognizedWord(CoreOcrWord word, string? languageTag)
    {
        if (!IsJapaneseLanguage(languageTag))
        {
            return word;
        }

        var japaneseLetterCount = word.Text.Count(IsJapaneseLetter);
        if (japaneseLetterCount == 0)
        {
            return null;
        }

        var foreignLetterCount = word.Text.Count(character =>
            char.IsLetter(character) &&
            !IsJapaneseLetter(character) &&
            !IsJapaneseTextCompanion(character));
        if (foreignLetterCount > 0 &&
            (japaneseLetterCount < MinimumJapaneseLetterCount ||
             japaneseLetterCount / (double)(japaneseLetterCount + foreignLetterCount) < MinimumJapaneseLetterRatio))
        {
            return null;
        }

        var filteredText = string.Concat(word.Text.Where(character =>
            IsJapaneseLetter(character) || IsJapaneseTextCompanion(character)));
        return filteredText.Length == 0
            ? null
            : word with { Text = filteredText };
    }

    public static bool IsJapaneseLanguage(string? languageTag)
    {
        return NormalizeLanguage(languageTag) == "ja";
    }

    private static string NormalizeLanguage(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return string.Empty;
        }

        var separatorIndex = languageTag.IndexOfAny(['-', '_']);
        return (separatorIndex > 0 ? languageTag[..separatorIndex] : languageTag)
            .Trim()
            .ToLowerInvariant();
    }

    private static bool HasSufficientJapaneseText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var totalLetterCount = text.Count(char.IsLetter);
        var japaneseLetterCount = text.Count(IsJapaneseLetter);
        return totalLetterCount > 0 &&
               japaneseLetterCount >= MinimumJapaneseLetterCount &&
               japaneseLetterCount / (double)totalLetterCount >= MinimumJapaneseLetterRatio;
    }

    internal static bool IsJapaneseLetter(char character)
    {
        return character is
            >= '\u3005' and <= '\u3007' or
            >= '\u3041' and <= '\u3096' or
            >= '\u309D' and <= '\u309F' or
            >= '\u30A1' and <= '\u30FA' or
            >= '\u30FD' and <= '\u30FF' or
            >= '\u31F0' and <= '\u31FF' or
            >= '\u3400' and <= '\u4DBF' or
            >= '\u4E00' and <= '\u9FFF' or
            >= '\uF900' and <= '\uFAFF' or
            >= '\uFF66' and <= '\uFF6F' or
            >= '\uFF71' and <= '\uFF9D';
    }

    private static bool IsJapaneseTextCompanion(char character)
    {
        return char.IsDigit(character) || character is
            '\u3001' or '\u3002' or
            >= '\u3008' and <= '\u3011' or
            >= '\u3014' and <= '\u301B' or
            '\u3099' or '\u309A' or
            '\u30FB' or '\u30FC' or
            >= '\uFF61' and <= '\uFF65' or
            '\uFF70' or '\uFF9E' or '\uFF9F' or
            '!' or '?' or ':' or ';' or
            '\uFF01' or '\uFF1F' or '\uFF1A' or '\uFF1B' or
            '(' or ')' or '[' or ']' or
            '\uFF08' or '\uFF09' or
            '\u2026' or '\u301C' or '\uFF5E' or
            '-' or '\u2015';
    }

    public static bool IsLikelyEnglish(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var letters = 0;
        var asciiLetters = 0;

        foreach (var character in text)
        {
            if (VietnameseDiacritics.Contains(character))
            {
                return false;
            }

            if (!char.IsLetter(character))
            {
                continue;
            }

            letters++;
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                asciiLetters++;
            }
        }

        if (letters == 0)
        {
            return false;
        }

        return asciiLetters / (double)letters >= 0.9;
    }

    private static bool LooksLikeVisualNoise(string text)
    {
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length == 0)
        {
            return true;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (letters.Length == 1)
        {
            return true;
        }

        var normalizedLetters = letters
            .Select(char.ToUpperInvariant)
            .ToArray();
        var distinctLetters = normalizedLetters.Distinct().Count();
        var vowelCount = normalizedLetters.Count(character => character is 'A' or 'E' or 'I' or 'O' or 'U');

        if (distinctLetters <= 1 && letters.Length <= 8)
        {
            return true;
        }

        if (words.Length >= 2 && distinctLetters <= 2 && vowelCount == letters.Length)
        {
            return true;
        }

        return false;
    }
}
