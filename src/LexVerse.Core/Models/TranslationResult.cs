namespace LexVerse.Core.Models;

public sealed record TokenLink(
    int Index,
    string SourceToken,
    string TargetToken,
    string? Note = null);

public sealed record TermAnnotation(
    string Term,
    string Meaning,
    string Description);

public sealed record SourceBounds(
    double X,
    double Y,
    double Width,
    double Height);

public sealed record TranslationSegment(
    int Index,
    string SourceText,
    string TranslatedText,
    SourceBounds? SourceBounds);

public sealed record ScreenTextSegment(
    int Index,
    string Text,
    SourceBounds Bounds);

public sealed record TranslationResult(
    string SourceText,
    string TranslatedText,
    IReadOnlyList<TokenLink> TokenLinks,
    IReadOnlyList<TermAnnotation> Annotations,
    IReadOnlyList<TranslationSegment>? Segments = null);
