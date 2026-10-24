using System.Diagnostics;

namespace LexVerse.Infrastructure.Configuration;

/// <summary>Opens only the public plans URL supplied by desktop configuration.</summary>
public sealed class PlansWebsite
{
    private readonly Uri? _uri;

    public PlansWebsite(string? configuredUrl)
    {
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host) &&
            string.IsNullOrEmpty(uri.UserInfo))
            _uri = uri;
    }

    public bool IsAvailable => _uri is not null;

    public void Open(Action<Uri>? launch = null)
    {
        if (_uri is null)
            throw new InvalidOperationException("plans-website-unavailable");

        if (launch is not null)
        {
            launch(_uri);
            return;
        }

        Process.Start(new ProcessStartInfo(_uri.AbsoluteUri) { UseShellExecute = true });
    }
}
