namespace LexVerse.Infrastructure.Configuration;

public static class BackendEndpointPolicy
{
    public static bool AllowInsecureLocalhost =>
        string.Equals(Environment.GetEnvironmentVariable("LEXVERSE_ENVIRONMENT"), "Development", StringComparison.Ordinal) &&
        string.Equals(Environment.GetEnvironmentVariable("LEXVERSE_ALLOW_INSECURE_LOCALHOST"), "true", StringComparison.OrdinalIgnoreCase);

    public static void LoadEnvironment() => DotEnvFile.LoadNearest();

    public static void Validate(Uri uri, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || !string.IsNullOrEmpty(uri.UserInfo) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(AllowInsecureLocalhost && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            throw new ArgumentException("Backend endpoints require HTTPS; HTTP loopback is allowed only in explicit development mode.", parameterName);
        }
    }
}
