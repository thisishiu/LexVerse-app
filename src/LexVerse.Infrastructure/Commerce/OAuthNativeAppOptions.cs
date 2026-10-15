namespace LexVerse.Infrastructure.Commerce;

public sealed record OAuthNativeAppOptions
{
    public OAuthNativeAppOptions(
        Uri authorizationEndpoint,
        Uri tokenEndpoint,
        Uri accountEndpoint,
        string clientId)
    {
        RemoteProductCatalogProvider.ValidateHttps(authorizationEndpoint, nameof(authorizationEndpoint));
        RemoteProductCatalogProvider.ValidateHttps(tokenEndpoint, nameof(tokenEndpoint));
        RemoteProductCatalogProvider.ValidateHttps(accountEndpoint, nameof(accountEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        AuthorizationEndpoint = authorizationEndpoint;
        TokenEndpoint = tokenEndpoint;
        AccountEndpoint = accountEndpoint;
        ClientId = clientId.Trim();
    }

    public Uri AuthorizationEndpoint { get; }
    public Uri TokenEndpoint { get; }
    public Uri AccountEndpoint { get; }
    public string ClientId { get; }
}
