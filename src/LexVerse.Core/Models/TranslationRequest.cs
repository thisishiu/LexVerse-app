namespace LexVerse.Core.Models;

public sealed record TranslationRequest(
    string SourceText,
    string SourceLanguage,
    string TargetLanguage,
    ScreenSelection? Selection = null,
    IReadOnlyList<ScreenTextSegment>? TextSegments = null);
