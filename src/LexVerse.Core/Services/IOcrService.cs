using LexVerse.Core.Models;

namespace LexVerse.Core.Services;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(ScreenSelection selection, CancellationToken cancellationToken = default);
}
