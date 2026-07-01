using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LexVerse.Infrastructure.Google;

public sealed class GoogleAccessTokenProvider
{
    private const string Scope = "https://www.googleapis.com/auth/cloud-platform";
    private readonly HttpClient _httpClient;
    private readonly GoogleServiceAccount _serviceAccount;
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public GoogleAccessTokenProvider(HttpClient httpClient, string credentialPath)
    {
        _httpClient = httpClient;
        var json = File.ReadAllText(credentialPath);
        _serviceAccount = JsonSerializer.Deserialize<GoogleServiceAccount>(json)
            ?? throw new InvalidOperationException("Google service account file is not valid JSON.");
    }

    public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && _expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        var assertion = CreateAssertion();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });

        using var response = await _httpClient.PostAsync(_serviceAccount.TokenUri, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google auth failed: {response.StatusCode}. {body}");
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(body)
            ?? throw new InvalidOperationException("Google auth returned an unreadable token response.");

        _accessToken = token.AccessToken;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

        return new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private string CreateAssertion()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT"
        }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = _serviceAccount.ClientEmail,
            scope = Scope,
            aud = _serviceAccount.TokenUri,
            iat = now,
            exp = now + 3600
        }));

        var unsigned = $"{header}.{payload}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_serviceAccount.PrivateKey);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
