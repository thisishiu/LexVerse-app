using LexVerse.Core.Ocr;

namespace LexVerse.OCR;

internal static class JapaneseRubyTextFilter
{
    private const double MaximumRubyHeightRatio = 0.68;
    private const double MinimumHorizontalOverlapRatio = 0.30;
    private const double MaximumVerticalGapFactor = 0.90;
    private const double MaximumVerticalOverlapFactor = 0.35;

    public static IReadOnlyList<OcrWord> RemoveLikelyAnnotations(
        IReadOnlyList<OcrWord> words,
        string? languageTag)
    {
        ArgumentNullException.ThrowIfNull(words);

        if (!IsJapanese(languageTag) || words.Count < 2)
        {
            return words;
        }

        return words
            .Where(candidate => !words.Any(baseWord =>
                !ReferenceEquals(candidate, baseWord) &&
                IsLikelyRubyAbove(candidate, baseWord)))
            .ToArray();
    }

    private static bool IsLikelyRubyAbove(OcrWord candidate, OcrWord baseWord)
    {
        var candidateHeight = Math.Max(1, candidate.Bounds.Height);
        var baseHeight = Math.Max(1, baseWord.Bounds.Height);
        if (candidateHeight > baseHeight * MaximumRubyHeightRatio ||
            CenterY(candidate.Bounds) >= CenterY(baseWord.Bounds))
        {
            return false;
        }

        var verticalGap = baseWord.Bounds.Y - candidate.Bounds.Bottom;
        if (verticalGap > Math.Max(6, baseHeight * MaximumVerticalGapFactor) ||
            verticalGap < -candidateHeight * MaximumVerticalOverlapFactor)
        {
            return false;
        }

        var horizontalOverlap = Math.Min(candidate.Bounds.Right, baseWord.Bounds.Right) -
                                Math.Max(candidate.Bounds.X, baseWord.Bounds.X);
        if (horizontalOverlap <= 0)
        {
            return false;
        }

        var narrowerWidth = Math.Max(1, Math.Min(candidate.Bounds.Width, baseWord.Bounds.Width));
        return horizontalOverlap / (double)narrowerWidth >= MinimumHorizontalOverlapRatio;
    }

    private static bool IsJapanese(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return false;
        }

        var separatorIndex = languageTag.IndexOfAny(['-', '_']);
        var normalized = separatorIndex > 0 ? languageTag[..separatorIndex] : languageTag;
        return normalized.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase);
    }

    private static double CenterY(BoundingBox bounds) => bounds.Y + bounds.Height / 2.0;
}
