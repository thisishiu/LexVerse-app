using LexVerse.Core.Models;

namespace LexVerse.Core.Services;

public interface ITextSegmentationService
{
    Task<IReadOnlyList<ScreenTextSegment>> DetectTextSegmentsAsync(
        ScreenSelection selection,
        CancellationToken cancellationToken = default);
}
