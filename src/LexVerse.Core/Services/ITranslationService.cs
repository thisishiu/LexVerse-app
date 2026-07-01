using LexVerse.Core.Models;

namespace LexVerse.Core.Services;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
}
