using System.Globalization;
using LexVerse.Core.Ocr;

namespace LexVerse.OCR;

internal static class JapaneseLatinTextVeto
{
    private const double MinimumVerticalOverlap = 0.52;
    private const double MinimumCandidateCoverage = 0.38;

    public static IReadOnlyList<OcrWord> RemoveLikelyLatinHallucinations(
        IReadOnlyList<OcrTextBlock> japaneseLines,
        IReadOnlyList<OcrTextBlock> latinLines,
        double japaneseTextAngle,
        double latinTextAngle,
        int frameWidth,
        int frameHeight)
    {
        if (japaneseLines.Count == 0 || latinLines.Count == 0)
        {
            return japaneseLines.SelectMany(line => line.Words).ToArray();
        }

        var latinEvidence = latinLines
            .Select(line => CreateLatinEvidence(
                line,
                latinTextAngle,
                frameWidth,
                frameHeight))
            .OfType<LatinEvidenceLine>()
            .ToArray();

        if (latinEvidence.Length == 0)
        {
            return japaneseLines.SelectMany(line => line.Words).ToArray();
        }

        var retainedWords = new List<OcrWord>();
        foreach (var japaneseLine in japaneseLines)
        {
            var normalizedLineBounds = NormalizeBounds(
                japaneseLine.Bounds,
                japaneseTextAngle,
                frameWidth,
                frameHeight);

            if (IsLatinDominant(
                japaneseLine.Text,
                normalizedLineBounds,
                latinEvidence,
                useWordEvidence: false))
            {
                continue;
            }

            foreach (var word in japaneseLine.Words)
            {
                var normalizedWordBounds = NormalizeBounds(
                    word.Bounds,
                    japaneseTextAngle,
                    frameWidth,
                    frameHeight);

                if (!IsLatinDominant(
                    word.Text,
                    normalizedWordBounds,
                    latinEvidence,
                    useWordEvidence: true))
                {
                    retainedWords.Add(word);
                }
            }
        }

        return retainedWords;
    }

    private static LatinEvidenceLine? CreateLatinEvidence(
        OcrTextBlock line,
        double textAngle,
        int frameWidth,
        int frameHeight)
    {
        var stats = GetScriptStats(line.Text);
        if (!HasStrongLatinEvidence(line.Text, stats))
        {
            return null;
        }

        var words = line.Words
            .Select(word =>
            {
                var wordStats = GetScriptStats(word.Text);
                return new LatinEvidenceWord(
                    NormalizeBounds(word.Bounds, textAngle, frameWidth, frameHeight),
                    wordStats);
            })
            .Where(word => word.Stats.LatinLetters >= 2 && word.Stats.LatinRatio >= 0.80)
            .ToArray();

        return new LatinEvidenceLine(
            NormalizeBounds(line.Bounds, textAngle, frameWidth, frameHeight),
            stats,
            IsShortUppercaseToken(line.Text),
            words);
    }

    private static bool IsLatinDominant(
        string japaneseText,
        NormalizedBounds candidateBounds,
        IReadOnlyList<LatinEvidenceLine> latinEvidence,
        bool useWordEvidence)
    {
        var japaneseLetterCount = japaneseText.Count(OcrTextLanguageFilter.IsJapaneseLetter);
        if (japaneseLetterCount == 0 || candidateBounds.Width <= 0 || candidateBounds.Height <= 0)
        {
            return false;
        }

        foreach (var evidence in latinEvidence)
        {
            if (!IsSameTextBand(candidateBounds, evidence.Bounds))
            {
                continue;
            }

            var comparison = useWordEvidence
                ? CompareWordEvidence(candidateBounds, evidence)
                : CompareLineEvidence(candidateBounds, evidence);
            if (comparison.CandidateCoverage < MinimumCandidateCoverage)
            {
                continue;
            }

            if (HasDominantLatinEvidence(
                comparison.LatinLetters,
                comparison.AccentedLatinLetters,
                japaneseLetterCount,
                evidence.IsShortUppercaseToken))
            {
                return true;
            }
        }

        return false;
    }

    private static EvidenceComparison CompareLineEvidence(
        NormalizedBounds candidate,
        LatinEvidenceLine evidence)
    {
        var overlapWidth = HorizontalOverlap(candidate, evidence.Bounds);
        if (overlapWidth <= 0 || evidence.Bounds.Width <= 0)
        {
            return default;
        }

        var evidenceCoverage = Math.Clamp(overlapWidth / evidence.Bounds.Width, 0, 1);
        return new EvidenceComparison(
            Math.Max(1, (int)Math.Round(evidence.Stats.LatinLetters * evidenceCoverage)),
            (int)Math.Round(evidence.Stats.AccentedLatinLetters * evidenceCoverage),
            Math.Clamp(overlapWidth / candidate.Width, 0, 1));
    }

    private static EvidenceComparison CompareWordEvidence(
        NormalizedBounds candidate,
        LatinEvidenceLine evidence)
    {
        var latinLetters = 0;
        var accentedLatinLetters = 0;
        var coveredIntervals = new List<HorizontalInterval>();

        foreach (var word in evidence.Words)
        {
            if (!IsSameTextBand(candidate, word.Bounds))
            {
                continue;
            }

            var overlapLeft = Math.Max(candidate.Left, word.Bounds.Left);
            var overlapRight = Math.Min(candidate.Right, word.Bounds.Right);
            var overlapWidth = overlapRight - overlapLeft;
            if (overlapWidth <= 0 || word.Bounds.Width <= 0)
            {
                continue;
            }

            var wordCoverage = Math.Clamp(overlapWidth / word.Bounds.Width, 0, 1);
            latinLetters += Math.Max(1, (int)Math.Round(word.Stats.LatinLetters * wordCoverage));
            accentedLatinLetters += (int)Math.Round(
                word.Stats.AccentedLatinLetters * wordCoverage);
            coveredIntervals.Add(new HorizontalInterval(overlapLeft, overlapRight));
        }

        if (latinLetters == 0)
        {
            return CompareLineEvidence(candidate, evidence);
        }

        return new EvidenceComparison(
            latinLetters,
            accentedLatinLetters,
            CalculateCoveredWidth(coveredIntervals) / candidate.Width);
    }

    private static bool HasDominantLatinEvidence(
        int latinLetters,
        int accentedLatinLetters,
        int japaneseLetters,
        bool isShortUppercaseToken)
    {
        if (latinLetters < 2)
        {
            return false;
        }

        if (accentedLatinLetters > 0 &&
            latinLetters >= Math.Max(2, (int)Math.Ceiling(japaneseLetters * 0.65)))
        {
            return true;
        }

        if (latinLetters >= 8 &&
            latinLetters >= Math.Max(3, (int)Math.Ceiling(japaneseLetters * 0.80)))
        {
            return true;
        }

        if (isShortUppercaseToken && latinLetters >= japaneseLetters)
        {
            return true;
        }

        return latinLetters >= Math.Max(3, (int)Math.Ceiling(japaneseLetters * 1.15));
    }

    private static bool HasStrongLatinEvidence(string text, ScriptStats stats)
    {
        if (stats.LatinLetters < 2 || stats.LatinRatio < 0.85)
        {
            return false;
        }

        if (stats.AccentedLatinLetters > 0 ||
            stats.LatinLetters >= 5 ||
            IsShortUppercaseToken(text))
        {
            return true;
        }

        var latinTokens = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(token => GetScriptStats(token).LatinLetters >= 2);
        return latinTokens >= 2 && stats.LatinLetters >= 4;
    }

    private static bool IsSameTextBand(NormalizedBounds left, NormalizedBounds right)
    {
        var verticalOverlap = Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top);
        if (verticalOverlap <= 0)
        {
            return false;
        }

        var smallerHeight = Math.Min(left.Height, right.Height);
        return smallerHeight > 0 && verticalOverlap / smallerHeight >= MinimumVerticalOverlap;
    }

    private static double HorizontalOverlap(NormalizedBounds left, NormalizedBounds right)
    {
        return Math.Max(0, Math.Min(left.Right, right.Right) - Math.Max(left.Left, right.Left));
    }

    private static double CalculateCoveredWidth(IReadOnlyList<HorizontalInterval> intervals)
    {
        if (intervals.Count == 0)
        {
            return 0;
        }

        var ordered = intervals.OrderBy(interval => interval.Left).ToArray();
        var coveredWidth = 0d;
        var currentLeft = ordered[0].Left;
        var currentRight = ordered[0].Right;

        foreach (var interval in ordered.Skip(1))
        {
            if (interval.Left <= currentRight)
            {
                currentRight = Math.Max(currentRight, interval.Right);
                continue;
            }

            coveredWidth += currentRight - currentLeft;
            currentLeft = interval.Left;
            currentRight = interval.Right;
        }

        return coveredWidth + currentRight - currentLeft;
    }

    private static ScriptStats GetScriptStats(string text)
    {
        var totalLetters = 0;
        var latinLetters = 0;
        var accentedLatinLetters = 0;

        foreach (var character in text)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            totalLetters++;
            if (!IsLatinLetter(character))
            {
                continue;
            }

            latinLetters++;
            if (character > '\u007F')
            {
                accentedLatinLetters++;
            }
        }

        return new ScriptStats(totalLetters, latinLetters, accentedLatinLetters);
    }

    private static bool IsShortUppercaseToken(string text)
    {
        var letters = text.Where(IsLatinLetter).ToArray();
        return letters.Length is >= 2 and <= 5 &&
               letters.All(character => char.GetUnicodeCategory(character) is
                   UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter);
    }

    private static bool IsLatinLetter(char character)
    {
        if (!char.IsLetter(character))
        {
            return false;
        }

        return character is
            >= '\u0041' and <= '\u005A' or
            >= '\u0061' and <= '\u007A' or
            >= '\u00C0' and <= '\u024F' or
            >= '\u1E00' and <= '\u1EFF' or
            >= '\uFF21' and <= '\uFF3A' or
            >= '\uFF41' and <= '\uFF5A';
    }

    internal static NormalizedBounds NormalizeBounds(
        BoundingBox bounds,
        double textAngle,
        int frameWidth,
        int frameHeight)
    {
        if (Math.Abs(textAngle) < 0.001)
        {
            return new NormalizedBounds(bounds.X, bounds.Y, bounds.Right, bounds.Bottom);
        }

        var radians = textAngle * Math.PI / 180;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var centerX = frameWidth / 2d;
        var centerY = frameHeight / 2d;
        var corners = new[]
        {
            Rotate(bounds.X, bounds.Y),
            Rotate(bounds.Right, bounds.Y),
            Rotate(bounds.Right, bounds.Bottom),
            Rotate(bounds.X, bounds.Bottom)
        };

        return new NormalizedBounds(
            corners.Min(point => point.X),
            corners.Min(point => point.Y),
            corners.Max(point => point.X),
            corners.Max(point => point.Y));

        NormalizedPoint Rotate(double x, double y)
        {
            var offsetX = x - centerX;
            var offsetY = y - centerY;
            return new NormalizedPoint(
                centerX + offsetX * cosine - offsetY * sine,
                centerY + offsetX * sine + offsetY * cosine);
        }
    }

    internal readonly record struct NormalizedBounds(
        double Left,
        double Top,
        double Right,
        double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }

    private readonly record struct NormalizedPoint(double X, double Y);

    private readonly record struct HorizontalInterval(double Left, double Right);

    private readonly record struct EvidenceComparison(
        int LatinLetters,
        int AccentedLatinLetters,
        double CandidateCoverage);

    private readonly record struct ScriptStats(
        int TotalLetters,
        int LatinLetters,
        int AccentedLatinLetters)
    {
        public double LatinRatio => TotalLetters == 0
            ? 0
            : LatinLetters / (double)TotalLetters;
    }

    private sealed record LatinEvidenceLine(
        NormalizedBounds Bounds,
        ScriptStats Stats,
        bool IsShortUppercaseToken,
        IReadOnlyList<LatinEvidenceWord> Words);

    private sealed record LatinEvidenceWord(
        NormalizedBounds Bounds,
        ScriptStats Stats);
}
