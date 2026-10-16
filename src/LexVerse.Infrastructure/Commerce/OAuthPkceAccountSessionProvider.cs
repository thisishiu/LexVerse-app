using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
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
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var expiration = new CancellationTokenSource(TimeSpan.FromMinutes(15), _timeProvider);
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiration.Token);
            _signInCancellation = attempt;
            cancellationToken = attempt.Token;
            SetSignInState("opening");
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var state = Base64Url(RandomNumberGenerator.GetBytes(32));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var redirectUri = new Uri($"http://127.0.0.1:{port}/oauth/callback/");
            PendingAuthorizationUri = BuildAuthorizationUri(redirectUri, challenge, state);
            ReopenBrowser();

            var stored = await ReceiveAuthorizationCodeAsync(listener, redirectUri, state, async code =>
            {
                SetSignInState("completing");
                var tokens = await ExchangeCodeAsync(code, verifier, redirectUri, cancellationToken);
                var profile = await GetProfileAsync(tokens.AccessToken, cancellationToken);
                var account = new StoredAccount
                {
                    SubjectId = profile.SubjectId,
                    DisplayName = profile.DisplayName,
                    AuthenticatedAtUtc = _timeProvider.GetUtcNow(),
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                    AccessTokenExpiresAtUtc = tokens.ExpiresAtUtc
                };
                await _store.WriteAsync(account, cancellationToken);
                return account;
            }, cancellationToken);
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

    private Uri BuildAuthorizationUri(Uri redirectUri, string challenge, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri.AbsoluteUri,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };
        var builder = new UriBuilder(_options.AuthorizationEndpoint)
        {
            Query = string.Join('&', query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))
        };
        return builder.Uri;
    }

    private async Task<StoredAccount> ReceiveAuthorizationCodeAsync(
        TcpListener listener,
        Uri redirectUri,
        string expectedState,
        Func<string, Task<StoredAccount>> complete,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(15));
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            requestDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                if (client.Client.RemoteEndPoint is not IPEndPoint { Address: var address } || !IPAddress.IsLoopback(address))
                {
                    throw new InvalidOperationException("The OAuth callback was not received from loopback.");
                }

                await using var stream = client.GetStream();
                var header = await ReadHttpHeaderAsync(stream, requestDeadline.Token);
                var firstLineEnd = header.IndexOf("\r\n", StringComparison.Ordinal);
                var requestLine = firstLineEnd < 0 ? header : header[..firstLineEnd];
                var parts = requestLine.Split(' ');
                if (parts.Length != 3 || parts[0] != "GET" || parts[1].Length > 8192)
                {
                    throw new InvalidOperationException("The OAuth callback request is invalid.");
                }

                var callback = new Uri($"{redirectUri.Scheme}://{redirectUri.Authority}{parts[1]}");
                if (!string.Equals(callback.AbsolutePath, redirectUri.AbsolutePath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The OAuth callback path is invalid.");
                }

                var parameters = ParseQuery(callback.Query);
                if (!parameters.TryGetValue("state", out var checkedState) || !FixedTimeEquals(expectedState, checkedState))
                {
                    await WriteBrowserResponseAsync(stream, false, requestDeadline.Token);
                    continue;
                }
                if (parameters.ContainsKey("error"))
                {
                    await WriteBrowserResponseAsync(stream, success: false, deadline.Token);
                    SetSignInState("error", "authorization-denied");
                    throw new OperationCanceledException("Authorization was declined.", cancellationToken);
                }

                if (!parameters.TryGetValue("code", out var code) ||
                    string.IsNullOrWhiteSpace(code) || code.Length > 4096)
                {
                    await WriteBrowserResponseAsync(stream, success: false, deadline.Token);
                    throw new InvalidOperationException("The OAuth callback validation failed.");
                }

                StoredAccount account;
                try { account = await complete(code); }
                catch
                {
                    try { await WriteBrowserResponseAsync(stream, false, deadline.Token); }
                    catch (Exception exception) when (exception is IOException or OperationCanceledException) { }
                    throw;
                }
                try { await WriteBrowserResponseAsync(stream, true, deadline.Token); }
                catch (Exception exception) when (exception is IOException or OperationCanceledException) { }
                return account;
            }
            catch (OperationCanceledException) when (SignInState.Status != "completing" && !deadline.IsCancellationRequested && requestDeadline.IsCancellationRequested) { }
            catch (InvalidOperationException) when (SignInState.Status != "completing") { }
            catch (UriFormatException) { }
            catch (IOException) when (SignInState.Status != "completing") { }
        }
    }

    private Task<TokenSet> ExchangeCodeAsync(
        string code,
        string verifier,
        Uri redirectUri,
        CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri.AbsoluteUri
        }, cancellationToken);

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

    private static async Task<string> ReadHttpHeaderAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>(1024);
        var one = new byte[1];
        while (bytes.Count < 16 * 1024)
        {
            if (await stream.ReadAsync(one, cancellationToken) == 0)
            {
                break;
            }

            bytes.Add(one[0]);
            if (bytes.Count >= 4 && bytes[^4] == '\r' && bytes[^3] == '\n' && bytes[^2] == '\r' && bytes[^1] == '\n')
            {
                return Encoding.ASCII.GetString([.. bytes]);
            }
        }

        throw new InvalidOperationException("The OAuth callback headers are invalid or too large.");
    }

    private static async Task WriteBrowserResponseAsync(
        NetworkStream stream,
        bool success,
        CancellationToken cancellationToken)
    {
        var title = success ? "Đã đăng nhập LexVerse" : "Chưa thể đăng nhập";
        var message = success ? "Tài khoản đã được lưu trong ứng dụng. Bạn có thể quay lại LexVerse." : "Quay lại ứng dụng LexVerse để xem trạng thái và thử lại.";
        var body = $"<!doctype html><meta charset=utf-8><title>{title}</title><h1>{title}</h1><p>{message}</p>";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(success ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString((separator < 0 ? pair : pair[..separator]).Replace('+', ' '));
            var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            if (!result.TryAdd(key, value))
            {
                throw new InvalidOperationException("The OAuth callback contains duplicate parameters.");
            }
        }

        return result;
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
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
