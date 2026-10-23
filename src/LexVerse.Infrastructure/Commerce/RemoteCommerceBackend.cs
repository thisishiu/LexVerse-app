using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexVerse.Application.Account;
using LexVerse.Application.Commerce;

namespace LexVerse.Infrastructure.Commerce;

public sealed class RemoteCommerceBackend : ICommerceBackend
{
    private const int MaximumResponseBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly HttpClient _httpClient;
    private readonly Uri _checkoutEndpoint;
    private readonly Uri _restoreEndpoint;
    private readonly IAccountSessionProvider _accounts;
    private readonly TimeSpan _timeout;

    public RemoteCommerceBackend(
        HttpClient httpClient,
        Uri checkoutEndpoint,
        Uri restoreEndpoint,
        IAccountSessionProvider accounts,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        RemoteProductCatalogProvider.ValidateHttps(checkoutEndpoint, nameof(checkoutEndpoint));
        RemoteProductCatalogProvider.ValidateHttps(restoreEndpoint, nameof(restoreEndpoint));
        ArgumentNullException.ThrowIfNull(accounts);
        _httpClient = httpClient;
        _checkoutEndpoint = checkoutEndpoint;
        _restoreEndpoint = restoreEndpoint;
        _accounts = accounts;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    public bool IsConfigured => _accounts.IsConfigured;

    public async Task<CheckoutSession> CreateCheckoutAsync(
        string offerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offerId);
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, _checkoutEndpoint, cancellationToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { offerId = offerId.Trim() }),
            Encoding.UTF8,
            "application/json");
        var bytes = await HttpJson.ReadBoundedAsync(
            _httpClient, request, MaximumResponseBytes, _timeout, cancellationToken);
        var response = JsonSerializer.Deserialize<CheckoutWire>(bytes, JsonOptions)
            ?? throw new InvalidOperationException("The checkout response is empty.");
        return new CheckoutSession(new Uri(response.CheckoutUri, UriKind.Absolute), response.ExpiresAtUtc,
            LexVerse.Infrastructure.Configuration.BackendEndpointPolicy.AllowInsecureLocalhost);
    }

    public async Task RestorePurchasesAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, _restoreEndpoint, cancellationToken);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        _ = await HttpJson.ReadBoundedAsync(
            _httpClient, request, MaximumResponseBytes, _timeout, cancellationToken);
    }

    public async Task<SubscriptionSummary?> GetSubscriptionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = new Uri(_checkoutEndpoint, "../me/subscription");
            using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, endpoint, cancellationToken);
            var bytes = await HttpJson.ReadBoundedAsync(
                _httpClient, request, MaximumResponseBytes, _timeout, cancellationToken);
            var summary = JsonSerializer.Deserialize<SubscriptionSummary>(bytes, JsonOptions);
            if (summary?.Current is null || summary.Upcoming is null ||
                !ValidPeriod(summary.Current) || summary.Upcoming.Any(period => !ValidPeriod(period)) ||
                summary.SyncedAtUtc == default || (summary.PendingCheckout is { } pending &&
                    (!Uri.TryCreate(pending.CheckoutUri, UriKind.Absolute, out var checkout) ||
                     !(checkout.Scheme == Uri.UriSchemeHttps || (checkout.IsLoopback &&
                         checkout.Scheme == Uri.UriSchemeHttp &&
                         LexVerse.Infrastructure.Configuration.BackendEndpointPolicy.AllowInsecureLocalhost)) ||
                     pending.ExpiresAtUtc == default)))
                throw new SubscriptionSyncException("subscription-invalid-response");
            return summary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException exception)
        {
            throw new SubscriptionSyncException("subscription-timeout", exception);
        }
        catch (HttpRequestException exception)
        {
            var code = exception.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "subscription-sign-in-required",
                System.Net.HttpStatusCode.NotFound => "subscription-endpoint-unavailable",
                _ => "subscription-unavailable"
            };
            throw new SubscriptionSyncException(code, exception);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            throw new SubscriptionSyncException("subscription-invalid-response", exception);
        }
    }

    private static bool ValidPeriod(SubscriptionPeriodSummary? period) => period is not null &&
        !string.IsNullOrWhiteSpace(period.Id) && !string.IsNullOrWhiteSpace(period.PlanId) &&
        !string.IsNullOrWhiteSpace(period.DisplayName) && period.StartsAtUtc != default &&
        period.EndsAtUtc > period.StartsAtUtc;

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        HttpMethod method,
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        var token = await _accounts.GetAccessTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private sealed class CheckoutWire
    {
        public required string CheckoutUri { get; init; }
        public required DateTimeOffset ExpiresAtUtc { get; init; }
    }
}
