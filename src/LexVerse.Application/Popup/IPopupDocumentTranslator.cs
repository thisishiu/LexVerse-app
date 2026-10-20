namespace LexVerse.Application.Popup;

public sealed record PopupTerm(string Term, string Translation, string Explanation);
public sealed record PopupDocument(string SourceText, string TranslationMarkdown, IReadOnlyList<PopupTerm> Terms);

public interface IPopupDocumentTranslator
{
    Task<PopupDocument> TranslateAsync(string? text, byte[]? png, PopupTranslationRequest request,
        CancellationToken cancellationToken = default);
}
