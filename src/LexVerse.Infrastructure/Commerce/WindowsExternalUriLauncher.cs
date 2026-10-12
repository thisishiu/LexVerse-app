using System.Diagnostics;
using LexVerse.Application.Commerce;

namespace LexVerse.Infrastructure.Commerce;

public sealed class WindowsExternalUriLauncher : IExternalUriLauncher
{
    public void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        LexVerse.Infrastructure.Configuration.BackendEndpointPolicy.Validate(uri, nameof(uri));
        if (uri.AbsoluteUri.Length > 8192)
        {
            throw new ArgumentException("Only absolute HTTPS URIs may be opened.", nameof(uri));
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
