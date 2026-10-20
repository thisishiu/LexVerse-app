using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LexVerse.Application.Desktop;

namespace LexVerse.Infrastructure.Desktop;

public sealed class PersistentDesktopPreferencesService : IDesktopPreferencesService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DesktopPreferences _current;
    private bool _disposed;

    public PersistentDesktopPreferencesService(string? path = null)
    {
        _path = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LexVerse",
            "Settings",
            "desktop.json"));
        _current = (Load() ?? new DesktopPreferences()).Normalize();
    }

    public DesktopPreferences Current => Volatile.Read(ref _current);

    public event EventHandler<DesktopPreferences>? Changed;

    public async Task UpdateAsync(
        DesktopPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(preferences);
        var normalized = preferences.Normalize();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("The desktop settings path has no directory.");
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                var wire = new DesktopPreferencesWire
                {
                    SchemaVersion = DesktopPreferences.CurrentSchemaVersion,
                    Preferences = normalized
                };
                await using (var stream = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(stream, wire, JsonOptions, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporaryPath, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            Volatile.Write(ref _current, normalized);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, normalized);
    }

    private DesktopPreferences? Load()
    {
        try
        {
            if (!File.Exists(_path) || new FileInfo(_path).Length is <= 0 or > 64 * 1024)
            {
                return null;
            }

            var document = JsonNode.Parse(File.ReadAllBytes(_path));
            // Retired settings must not invalidate the user's remaining preferences.
            if (document is JsonObject root && root["preferences"] is JsonObject preferences)
            {
                preferences.Remove("maxOcrFragments");
                preferences.Remove("showPopupBeforeTranslationFinishes");
            }

            var wire = document.Deserialize<DesktopPreferencesWire>(JsonOptions);
            return wire is { SchemaVersion: DesktopPreferences.CurrentSchemaVersion, Preferences: not null }
                ? wire.Preferences
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    private sealed class DesktopPreferencesWire
    {
        public required int SchemaVersion { get; init; }

        public required DesktopPreferences? Preferences { get; init; }
    }
}
