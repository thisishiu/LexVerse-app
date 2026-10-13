namespace LexVerse.Application.Desktop;

public interface IDesktopPreferencesService
{
    DesktopPreferences Current { get; }

    event EventHandler<DesktopPreferences>? Changed;

    Task UpdateAsync(
        DesktopPreferences preferences,
        CancellationToken cancellationToken = default);
}
