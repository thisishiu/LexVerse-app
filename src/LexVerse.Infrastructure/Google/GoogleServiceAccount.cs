using System.Text.Json.Serialization;

namespace LexVerse.Infrastructure.Google;

public sealed class GoogleServiceAccount
{
    [JsonPropertyName("client_email")]
    public string ClientEmail { get; init; } = string.Empty;

    [JsonPropertyName("private_key")]
    public string PrivateKey { get; init; } = string.Empty;

    [JsonPropertyName("token_uri")]
    public string TokenUri { get; init; } = "https://oauth2.googleapis.com/token";
}
