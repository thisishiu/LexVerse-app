namespace LexVerse.Infrastructure.Commerce;

public sealed record OAuthNativeAppOptions
{
    public OAuthNativeAppOptions(
        Uri desktopRequestEndpoint,
        Uri tokenEndpoint,
        Uri accountEndpoint,
        string clientId)
    {
        RemoteProductCatalogProvider.ValidateHttps(desktopRequestEndpoint, nameof(desktopRequestEndpoint));
        RemoteProductCatalogProvider.ValidateHttps(tokenEndpoint, nameof(tokenEndpoint));
        RemoteProductCatalogProvider.ValidateHttps(accountEndpoint, nameof(accountEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        DesktopRequestEndpoint = desktopRequestEndpoint;
        DesktopTokenEndpoint = new Uri(desktopRequestEndpoint.AbsoluteUri.TrimEnd('/') + "/token");
        TokenEndpoint = tokenEndpoint;
        AccountEndpoint = accountEndpoint;
        ClientId = clientId.Trim();
    }

    public Uri DesktopRequestEndpoint { get; }

    public Uri DesktopTokenEndpoint { get; }
    public Uri TokenEndpoint { get; }
    public Uri AccountEndpoint { get; }
    public string ClientId { get; }
}
