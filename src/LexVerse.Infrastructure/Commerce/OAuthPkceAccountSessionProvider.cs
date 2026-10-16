using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexVerse.Application.Account;
using LexVerse.Application.Commerce;

namespace LexVerse.Infrastructure.Commerce;

public sealed class OAuthPkceAccountSessionProvider : IAccountSessionProvider, IBrowserSignIn, IDisposable
{
    private const int MaximumOAuthResponseBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly JsonSerializerOptions TokenJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };
    private readonly HttpClient _httpClient;
    private readonly OAuthNativeAppOptions _options;
    private readonly WindowsDpapiAccountStore _store;
    private readonly IExternalUriLauncher _launcher;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private CancellationTokenSource? _signInCancellation;
    public BrowserSignInState SignInState { get; private set; } = new();
    public event EventHandler<BrowserSignInState>? SignInStateChanged;
    public Uri? PendingAuthorizationUri { get; private set; }

    private void SetSignInState(string status, string? error = null)
    {
        SignInState = new(status, error);
        SignInStateChanged?.Invoke(this, SignInState);
    }

    public void ReopenBrowser()
    {
        if (PendingAuthorizationUri is not { } uri) return;
        try { _launcher.Open(uri); SetSignInState("waiting"); }
        catch { SetSignInState("waiting", "browser-open-failed"); }
    }

    public void CancelSignIn() => _signInCancellation?.Cancel();

    public OAuthPkceAccountSessionProvider(
        HttpClient httpClient,
        OAuthNativeAppOptions options,
        WindowsDpapiAccountStore store,
        IExternalUriLauncher launcher,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(launcher);
        _httpClient = httpClient;
        _options = options;
        _store = store;
        _launcher = launcher;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsConfigured => true;

    public async Task<AccountSession?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var lifetime = CreateLifetimeToken(cancellationToken);
        cancellationToken = lifetime.Token;
        var stored = await _store.ReadAsync(cancellationToken);
        return stored is null ? null : ToSession(stored);
    }

    public async Task<AccountSession> SignInAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var lifetime = CreateLifetimeToken(cancellationToken);
        cancellationToken = lifetime.Token;
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("An account operation is already in progress.");
        try
        {
            using var expiration = new CancellationTokenSource(TimeSpan.FromMinutes(15), _timeProvider);
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiration.Token);
            _signInCancellation = attempt;
            cancellationToken = attempt.Token;
            SetSignInState("opening");
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var pending = await CreateDesktopRequestAsync(challenge, cancellationToken);
            expiration.CancelAfter(TimeSpan.FromSeconds(pending.ExpiresIn));
            PendingAuthorizationUri = new Uri(pending.VerificationUri);
            ReopenBrowser();
            var tokens = await PollDesktopRequestAsync(pending, verifier, cancellationToken);
            SetSignInState("completing");
            var profile = await GetProfileAsync(tokens.AccessToken, cancellationToken);
            var stored = new StoredAccount
            {
                SubjectId = profile.SubjectId,
                DisplayName = profile.DisplayName,
                AuthenticatedAtUtc = _timeProvider.GetUtcNow(),
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiresAtUtc = tokens.ExpiresAtUtc
            };
            await _store.WriteAsync(stored, cancellationToken);
            SetSignInState("success");
            return ToSession(stored);
        }
        catch (OperationCanceledException)
        {
            SetSignInState("error", "sign-in-cancelled-or-expired");
            throw;
        }
        catch
        {
            SetSignInState("error", "sign-in-failed");
            throw;
        }
        finally
        {
            PendingAuthorizationUri = null;
            _signInCancellation = null;
            _gate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var lifetime = CreateLifetimeToken(cancellationToken);
        cancellationToken = lifetime.Token;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var stored = await _store.ReadAsync(cancellationToken);
            var endpointText = Environment.GetEnvironmentVariable("LEXVERSE_LOGOUT_ENDPOINT");
            if (stored is not null && Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
            {
                RemoteProductCatalogProvider.ValidateHttps(endpoint, nameof(endpoint));
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stored.AccessToken);
                if (!string.IsNullOrWhiteSpace(stored.RefreshToken))
                {
                    request.Content = new StringContent(JsonSerializer.Serialize(new { refresh_token = stored.RefreshToken }), Encoding.UTF8, "application/json");
                }
                try
                {
                    _ = await HttpJson.ReadBoundedAsync(_httpClient, request, MaximumOAuthResponseBytes,
                        TimeSpan.FromSeconds(10), cancellationToken);
                }
                finally
                {
                    _store.Delete();
                }
                return;
            }
            _store.Delete();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var lifetime = CreateLifetimeToken(cancellationToken);
        cancellationToken = lifetime.Token;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var stored = await _store.ReadAsync(cancellationToken)
                ?? throw new InvalidOperationException("Sign in is required.");
            if (stored.AccessTokenExpiresAtUtc > _timeProvider.GetUtcNow().AddMinutes(1))
            {
                return stored.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(stored.RefreshToken))
            {
                throw new InvalidOperationException("The account session has expired. Sign in again.");
            }

            var tokens = await RefreshAsync(stored.RefreshToken, cancellationToken);
            var refreshed = new StoredAccount
            {
                SubjectId = stored.SubjectId,
                DisplayName = stored.DisplayName,
                AuthenticatedAtUtc = stored.AuthenticatedAtUtc,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken ?? stored.RefreshToken,
                AccessTokenExpiresAtUtc = tokens.ExpiresAtUtc
            };
            await _store.WriteAsync(refreshed, cancellationToken);
            return refreshed.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DesktopRequestWire> CreateDesktopRequestAsync(string challenge, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.DesktopRequestEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId, ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256"
            })
        };
        var bytes = await HttpJson.ReadBoundedAsync(_httpClient, request, MaximumOAuthResponseBytes,
            TimeSpan.FromSeconds(15), cancellationToken);
        var result = JsonSerializer.Deserialize<DesktopRequestWire>(bytes, TokenJsonOptions)
            ?? throw new InvalidOperationException("The sign-in response is empty.");
        if (!Uri.TryCreate(result.VerificationUri, UriKind.Absolute, out var uri) ||
            uri.Scheme != _options.DesktopRequestEndpoint.Scheme || uri.Authority != _options.DesktopRequestEndpoint.Authority ||
            !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(result.RequestSecret) || result.RequestSecret.Length != 43 ||
            result.ExpiresIn is <= 0 or > 900 || result.Interval is < 3 or > 60)
            throw new InvalidOperationException("The sign-in response is invalid.");
        return result;
    }

    private async Task<TokenSet> PollDesktopRequestAsync(DesktopRequestWire pending, string verifier, CancellationToken cancellationToken)
    {
        var interval = pending.Interval;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), _timeProvider, cancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.DesktopTokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client_id"] = _options.ClientId, ["request_secret"] = pending.RequestSecret,
                        ["code_verifier"] = verifier
                    })
                };
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                var bytes = await HttpJson.ReadResponseBoundedAsync(response, MaximumOAuthResponseBytes, deadline.Token);
                if (response.IsSuccessStatusCode) return ParseTokens(bytes);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    interval = Math.Min(interval + 5, 60);
                    continue;
                }
                using var error = JsonDocument.Parse(bytes);
                var code = error.RootElement.GetProperty("error").GetString();
                if (code == "authorization_pending") continue;
                if (code == "slow_down") { interval = Math.Min(interval + 5, 60); continue; }
                if (code == "expired_token") throw new InvalidOperationException("Sign-in expired. Start again from the app.");
                throw new InvalidOperationException("Sign-in request is no longer valid.");
            }
            catch (HttpRequestException) { interval = Math.Min(interval + 5, 60); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                interval = Math.Min(interval + 5, 60);
            }
        }
    }

    private Task<TokenSet> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["refresh_token"] = refreshToken
        }, cancellationToken);

    private async Task<TokenSet> RequestTokensAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        var bytes = await HttpJson.ReadBoundedAsync(
            _httpClient, request, MaximumOAuthResponseBytes, TimeSpan.FromSeconds(15), cancellationToken);
        return ParseTokens(bytes);
    }

    private TokenSet ParseTokens(byte[] bytes)
    {
        var response = JsonSerializer.Deserialize<TokenWire>(bytes, TokenJsonOptions)
            ?? throw new InvalidOperationException("The token response is empty.");
        if (!string.Equals(response.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(response.AccessToken) || response.AccessToken.Length > 16 * 1024 ||
            response.RefreshToken?.Length > 16 * 1024 || response.ExpiresIn is <= 0 or > 31_536_000)
        {
            throw new InvalidOperationException("The token response is invalid.");
        }

        return new TokenSet(
            response.AccessToken,
            response.RefreshToken,
            _timeProvider.GetUtcNow().AddSeconds(response.ExpiresIn));
    }

    private async Task<AccountWire> GetProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.AccountEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var bytes = await HttpJson.ReadBoundedAsync(
            _httpClient, request, MaximumOAuthResponseBytes, TimeSpan.FromSeconds(10), cancellationToken);
        var profile = JsonSerializer.Deserialize<AccountWire>(bytes, JsonOptions)
            ?? throw new InvalidOperationException("The account response is empty.");
        if (string.IsNullOrWhiteSpace(profile.SubjectId) || profile.SubjectId.Length > 256 ||
            string.IsNullOrWhiteSpace(profile.DisplayName) || profile.DisplayName.Length > 256)
        {
            throw new InvalidOperationException("The account response is invalid.");
        }

        return profile;
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static AccountSession ToSession(StoredAccount stored) =>
        new(stored.SubjectId, stored.DisplayName, stored.AuthenticatedAtUtc);

    private CancellationTokenSource CreateLifetimeToken(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
    }

    private sealed class DesktopRequestWire
    {
        [JsonPropertyName("request_secret")] public required string RequestSecret { get; init; }
        [JsonPropertyName("verification_uri")] public required string VerificationUri { get; init; }
        [JsonPropertyName("expires_in")] public required int ExpiresIn { get; init; }
        [JsonPropertyName("interval")] public required int Interval { get; init; }
    }

    private sealed class TokenWire
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
        [JsonPropertyName("token_type")]
        public required string TokenType { get; init; }
        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; init; }
    }

    private sealed class AccountWire
    {
        public required string SubjectId { get; init; }
        public required string DisplayName { get; init; }
    }

    private sealed record TokenSet(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAtUtc);
}
