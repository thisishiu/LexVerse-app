using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LexVerse.Application.Account;
using LexVerse.Application.Commerce;
using LexVerse.Application.Product;
using LexVerse.Core.Product;
using LexVerse.Infrastructure.Commerce;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class CommerceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Coordinator_PreservesConfirmedPlanAndTimestampOnFailureAndRecovers()
    {
        var token = TestContext.Current.CancellationToken;
        var backend = new StubCommerceBackend();
        var clock = new ManualClock();
        using var coordinator = new CommerceCoordinator(new StubAccounts(new AccountSession("customer-1", "Reader", Now)),
            new FixedCatalogProvider(CreateCatalog()), backend, new RecordingLauncher(), new RecordingAccessService(), clock);
        await coordinator.InitializeAsync(token);
        var confirmed = coordinator.State.Subscription;
        var lastSynced = coordinator.State.LastSyncedAtUtc;
        clock.Advance(TimeSpan.FromSeconds(1));
        backend.SubscriptionFailure = new SubscriptionSyncException("subscription-sign-in-required");
        await Assert.ThrowsAsync<SubscriptionSyncException>(() => coordinator.RestorePurchasesAsync(token));
        Assert.Same(confirmed, coordinator.State.Subscription);
        Assert.Equal(lastSynced, coordinator.State.LastSyncedAtUtc);
        Assert.Equal("subscription-sign-in-required", coordinator.State.SubscriptionError);
        backend.SubscriptionFailure = null;
        backend.FailRestore = true;
        await coordinator.RestorePurchasesAsync(token);
        Assert.Equal("free", coordinator.State.Subscription!.Current.PlanId);
        Assert.True(coordinator.State.SyncError);
        Assert.Null(coordinator.State.SubscriptionError);
        Assert.NotEqual(lastSynced, coordinator.State.LastSyncedAtUtc);
    }

    [Fact]
    public async Task Coordinator_FirstSyncFailureDoesNotInventFreeOrSuccessfulTimestamp()
    {
        var backend = new StubCommerceBackend { Subscription = null };
        using var coordinator = new CommerceCoordinator(new StubAccounts(new AccountSession("customer-1", "Reader", Now)),
            new FixedCatalogProvider(CreateCatalog()), backend, new RecordingLauncher(), new RecordingAccessService());
        await Assert.ThrowsAsync<SubscriptionSyncException>(() => coordinator.InitializeAsync(TestContext.Current.CancellationToken));
        Assert.Null(coordinator.State.Subscription);
        Assert.Null(coordinator.State.LastSyncedAtUtc);
        Assert.Equal("subscription-invalid-response", coordinator.State.SubscriptionError);
    }

    [Theory]
    [InlineData(401, "subscription-sign-in-required")]
    [InlineData(404, "subscription-endpoint-unavailable")]
    [InlineData(503, "subscription-unavailable")]
    [InlineData(200, "subscription-invalid-response")]
    public async Task RemoteSubscription_ClassifiesFailuresWithoutExposingResponse(int status, string code)
    {
        using var client = new HttpClient(new SubscriptionFailureHandler((HttpStatusCode)status));
        var backend = new RemoteCommerceBackend(client, new Uri("https://api.example.test/billing/checkout"),
            new Uri("https://api.example.test/billing/restore"), new StubAccounts(null));
        var error = await Assert.ThrowsAsync<SubscriptionSyncException>(() => backend.GetSubscriptionAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code);
        Assert.Equal(code, error.Message);
    }

    [Fact]
    public async Task RemoteSubscription_DistinguishesTimeoutFromCallerCancellation()
    {
        using var client = new HttpClient(new SubscriptionFailureHandler(HttpStatusCode.OK, hang: true));
        var backend = new RemoteCommerceBackend(client, new Uri("https://api.example.test/billing/checkout"),
            new Uri("https://api.example.test/billing/restore"), new StubAccounts(null), TimeSpan.FromMilliseconds(50));
        var error = await Assert.ThrowsAsync<SubscriptionSyncException>(() => backend.GetSubscriptionAsync(TestContext.Current.CancellationToken));
        Assert.Equal("subscription-timeout", error.Code);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => backend.GetSubscriptionAsync(cancellation.Token));
    }

    private sealed class SubscriptionFailureHandler(HttpStatusCode status, bool hang = false) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/me/subscription", request.RequestUri!.AbsolutePath);
            if (hang) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new(status) { Content = new StringContent("invalid response with private details") };
        }
    }

    [Fact]
    public async Task RemoteCommerce_PostmanSimulationUsesRealHttpForPendingPaidAndUnavailable()
    {
        var endpoint = Environment.GetEnvironmentVariable("LEXVERSE_COMMERCE_SIMULATION_URL");
        Assert.SkipWhen(string.IsNullOrEmpty(endpoint), "Set LEXVERSE_COMMERCE_SIMULATION_URL to the local Postman simulation.");
        var token = TestContext.Current.CancellationToken;
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-mock-slug", "subscription");
        await client.PostAsync(endpoint + "/testing/reset", null, token);
        var backend = new RemoteCommerceBackend(client, new Uri(endpoint + "/billing/checkout"),
            new Uri(endpoint + "/billing/restore"), new StubAccounts(null));
        Assert.Equal("free", (await backend.GetSubscriptionAsync(token))!.Current.PlanId);
        await backend.CreateCheckoutAsync("pro:1", token);
        var pending = await backend.GetSubscriptionAsync(token);
        Assert.Equal("free", pending!.Current.PlanId);
        Assert.NotNull(pending.PendingCheckout);
        await client.PostAsync(endpoint + "/testing/settle", null, token);
        await backend.RestorePurchasesAsync(token);
        var paid = await backend.GetSubscriptionAsync(token);
        Assert.Equal("pro", paid!.Current.PlanId);
        Assert.Null(paid.PendingCheckout);
        await client.PostAsync(endpoint + "/testing/unavailable", null, token);
        var failure = await Assert.ThrowsAsync<SubscriptionSyncException>(() => backend.GetSubscriptionAsync(token));
        Assert.Equal("subscription-unavailable", failure.Code);
    }

    [Fact]
    public async Task Coordinator_AutomaticallySyncsNormalAndCheckoutIntervalsAndStopsOnSignOut()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualClock();
        var backend = new StubCommerceBackend { Subscription = new(
            new("free-period", "free", "Free", Now, Now.AddMonths(1)), [], Now) };
        using var coordinator = new CommerceCoordinator(
            new StubAccounts(new AccountSession("customer-1", "Reader", Now)),
            new FixedCatalogProvider(CreateCatalog()), backend, new RecordingLauncher(), new RecordingAccessService(), clock);
        await coordinator.InitializeAsync(token);
        Assert.Equal("free", coordinator.State.Subscription!.Current.PlanId);
        Assert.Equal(1, backend.RestoreCount);
        await AdvanceToSync(TimeSpan.FromMinutes(5));
        Assert.Equal(2, backend.RestoreCount);
        await coordinator.StartCheckoutAsync("pro-monthly", token);
        Assert.Equal("free", coordinator.State.Subscription.Current.PlanId);
        await AdvanceToSync(TimeSpan.FromSeconds(10));
        Assert.NotNull(coordinator.State.PendingCheckout);
        backend.FailRestore = true;
        await AdvanceToSync(TimeSpan.FromSeconds(10));
        Assert.True(coordinator.State.SyncError);
        Assert.Equal("free", coordinator.State.Subscription.Current.PlanId);
        backend.FailRestore = false;
        backend.Subscription = new(new("paid-period", "pro", "Pro", Now, Now.AddMonths(1)), [], clock.GetUtcNow());
        await AdvanceToSync(TimeSpan.FromSeconds(10));
        Assert.Null(coordinator.State.PendingCheckout);
        Assert.Equal("pro", coordinator.State.Subscription.Current.PlanId);
        Assert.False(coordinator.State.SyncError);
        await coordinator.StartCheckoutAsync("pro-monthly", token);
        await AdvanceToSync(TimeSpan.FromMinutes(11));
        Assert.Null(coordinator.State.PendingCheckout);
        await coordinator.SignOutAsync(token);
        var count = backend.RestoreCount;
        clock.Advance(TimeSpan.FromMinutes(10));
        await Task.Yield();
        Assert.Equal(count, backend.RestoreCount);
        Assert.Null(coordinator.State.Subscription);

        async Task AdvanceToSync(TimeSpan elapsed)
        {
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var previous = coordinator.State.LastSyncedAtUtc;
            void Changed(object? sender, CommerceState state)
            {
                if (state.LastSyncedAtUtc != previous) completed.TrySetResult();
            }
            coordinator.StateChanged += Changed;
            try { clock.Advance(elapsed); await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), token); }
            finally { coordinator.StateChanged -= Changed; }
        }
    }

    [Fact]
    public async Task Coordinator_DoesNotOverlapManualAndScheduledSyncAndDisposalStopsTimer()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualClock();
        var backend = new StubCommerceBackend();
        var coordinator = new CommerceCoordinator(new StubAccounts(new AccountSession("customer-1", "Reader", Now)),
            new FixedCatalogProvider(CreateCatalog()), backend, new RecordingLauncher(), new RecordingAccessService(), clock);
        using (coordinator)
        {
            await coordinator.InitializeAsync(token);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            backend.RestoreBarrier = release.Task;
            var manual = coordinator.RestorePurchasesAsync(token);
            clock.Advance(TimeSpan.FromMinutes(5));
            await Task.Delay(50, token); // Let the scheduled continuation compete with the held gate.
            Assert.Equal(1, backend.MaximumConcurrentRestores);
            release.SetResult();
            await manual;
        }
        var count = backend.RestoreCount;
        clock.Advance(TimeSpan.FromMinutes(10));
        await Task.Yield();
        Assert.Equal(count, backend.RestoreCount);
    }

    [Fact]
    public async Task RemoteSubscription_ParsesGrantedPeriodWithoutTreatingPendingCheckoutAsAccess()
    {
        const string json = """
            {"current":{"id":"free-period","planId":"free","displayName":"Free","startsAtUtc":"2026-07-01T00:00:00Z","endsAtUtc":"2026-08-01T00:00:00Z"},"upcoming":[],"syncedAtUtc":"2026-07-20T12:00:00Z","pendingCheckout":{"checkoutUri":"https://pay.example.test/pending","expiresAtUtc":"2026-07-20T12:30:00Z"}}
            """;
        using var client = new HttpClient(new StaticHandler(json));
        var backend = new RemoteCommerceBackend(client, new Uri("https://api.example.test/billing/checkout"),
            new Uri("https://api.example.test/billing/restore"), new StubAccounts(null));
        var summary = await backend.GetSubscriptionAsync(TestContext.Current.CancellationToken);
        Assert.Equal("free", summary!.Current.PlanId);
        Assert.NotNull(summary.PendingCheckout);
    }

    [Fact]
    public async Task Coordinator_LoadsPublicCatalogWhileSignedOutAndRequiresSignInForCheckout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var catalog = CreateCatalog();
        var accounts = new StubAccounts(session: null);
        var backend = new StubCommerceBackend();
        var launcher = new RecordingLauncher();
        var access = new RecordingAccessService();
        using var coordinator = new CommerceCoordinator(
            accounts, new FixedCatalogProvider(catalog), backend, launcher, access);

        await coordinator.InitializeAsync(cancellationToken);

        Assert.Equal(CommerceStatus.SignedOut, coordinator.State.Status);
        Assert.Same(catalog, coordinator.State.Catalog);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.StartCheckoutAsync("pro-monthly", cancellationToken));
        Assert.Equal(0, backend.CheckoutCount);
    }

    [Fact]
    public async Task Coordinator_CheckoutUsesKnownOfferAndSignOutInvalidatesPaidAccess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new StubAccounts(new AccountSession("customer-1", "Reader", Now));
        var backend = new StubCommerceBackend();
        var launcher = new RecordingLauncher();
        var access = new RecordingAccessService();
        using var coordinator = new CommerceCoordinator(
            accounts, new FixedCatalogProvider(CreateCatalog()), backend, launcher, access);
        await coordinator.InitializeAsync(cancellationToken);

        await coordinator.StartCheckoutAsync("pro-monthly", cancellationToken);
        await coordinator.SignOutAsync(cancellationToken);

        Assert.Equal("pro-monthly", backend.LastOfferId);
        Assert.Equal(new Uri("https://checkout.example.test/session"), launcher.LastUri);
        Assert.Equal(1, access.InvalidateCount);
        Assert.Equal(CommerceStatus.SignedOut, coordinator.State.Status);
    }

    [Fact]
    public async Task RemoteCatalog_MapsRemotePlansPricesAndPeriods()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string json = """
            {
              "revision":"catalog-42",
              "publishedAtUtc":"2026-07-20T12:00:00Z",
              "plans":[{
                "id":"pro","displayName":"LexVerse Pro",
                "features":["translation.region","translation.full-screen"],"isPublic":true
              }],
              "offers":[{
                "id":"pro-monthly","planId":"pro","amount":99000,"currencyCode":"VND",
                "billingPeriod":{"count":1,"unit":"Month"},
                "trialPeriod":{"count":7,"unit":"Day"},"isPublic":true
              }]
            }
            """;
        using var client = new HttpClient(new StaticHandler(json));
        var provider = new RemoteProductCatalogProvider(
            client, new Uri("https://api.example.test/v1/catalog"));

        var catalog = await provider.GetCatalogAsync(cancellationToken);

        var offer = Assert.Single(catalog.Offers);
        Assert.Equal(99_000m, offer.Price.Amount);
        Assert.Equal("VND", offer.Price.CurrencyCode);
        Assert.Equal(SubscriptionPeriodUnit.Month, offer.BillingPeriod?.Unit);
        Assert.Equal(2, catalog.FindPlan("pro")?.Features.Count);
    }

    [Fact]
    public async Task OAuthPkce_UsesS256HttpsPollingAndPersistsOnlyDpapiCiphertext()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-oauth-{Guid.NewGuid():N}");
        var storePath = Path.Combine(directory, "session.dat");
        var handler = new OAuthHandler();
        using var client = new HttpClient(handler);
        var launcher = new CallbackLauncher();
        using var provider = new OAuthPkceAccountSessionProvider(
            client,
            new OAuthNativeAppOptions(
                new Uri("https://identity.example.test/authorize"),
                new Uri("https://identity.example.test/token"),
                new Uri("https://api.example.test/v1/account"),
                "lexverse-desktop"),
            new WindowsDpapiAccountStore(storePath),
            launcher,
            new FixedTimeProvider(Now));

        var session = await provider.SignInAsync(cancellationToken);

        Assert.Equal("customer-1", session.SubjectId);
        Assert.Equal("browser-code", launcher.Parameters["request"]);
        Assert.Equal("S256", handler.CreateForm["code_challenge_method"]);
        Assert.Equal(handler.CreateForm["code_challenge"], PkceChallenge(handler.CodeVerifier));
        Assert.DoesNotContain("redirect_uri", handler.CreateForm.Keys);
        Assert.DoesNotContain("client_secret", handler.TokenForm.Keys);
        Assert.DoesNotContain("grant_type", handler.TokenForm.Keys);
        var storedBytes = await File.ReadAllBytesAsync(storePath, cancellationToken);
        Assert.DoesNotContain("access-token-secret", Encoding.UTF8.GetString(storedBytes));
        Assert.Equal("access-token-secret", await provider.GetAccessTokenAsync(cancellationToken));

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task RemoteCommerce_SendsBearerAndIdempotencyButNotClientPrice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new CommerceHandler();
        using var client = new HttpClient(handler);
        var backend = new RemoteCommerceBackend(
            client,
            new Uri("https://api.example.test/v1/checkout"),
            new Uri("https://api.example.test/v1/restore"),
            new StubAccounts(new AccountSession("customer-1", "Reader", Now)));

        var checkout = await backend.CreateCheckoutAsync("pro-monthly", cancellationToken);
        await backend.RestorePurchasesAsync(cancellationToken);

        Assert.Equal(new Uri("https://checkout.example.test/session"), checkout.CheckoutUri);
        Assert.Equal("Bearer", handler.CheckoutAuthorization?.Scheme);
        Assert.Equal("token", handler.CheckoutAuthorization?.Parameter);
        Assert.False(string.IsNullOrWhiteSpace(handler.IdempotencyKey));
        Assert.Contains("\"offerId\":\"pro-monthly\"", handler.CheckoutBody, StringComparison.Ordinal);
        Assert.DoesNotContain("amount", handler.CheckoutBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task OAuthPkce_DisposeCancelsPendingBrowserSignIn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-oauth-{Guid.NewGuid():N}");
        var launcher = new NoCallbackLauncher();
        using var client = new HttpClient(new OAuthHandler());
        var provider = new OAuthPkceAccountSessionProvider(
            client,
            new OAuthNativeAppOptions(
                new Uri("https://identity.example.test/authorize"),
                new Uri("https://identity.example.test/token"),
                new Uri("https://api.example.test/v1/account"),
                "lexverse-desktop"),
            new WindowsDpapiAccountStore(Path.Combine(directory, "session.dat")),
            launcher);
        var pending = provider.SignInAsync(cancellationToken);
        await launcher.Opened.Task.WaitAsync(cancellationToken);

        provider.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string PkceChallenge(string verifier) => Base64Url(
        SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static ProductCatalog CreateCatalog()
    {
        var plan = new ProductPlan(
            "pro", "LexVerse Pro", [ProductFeatures.RegionTranslation, ProductFeatures.FullScreenTranslation]);
        var offer = new ProductOffer(
            "pro-monthly", plan.Id, new Money(99_000m, "VND"),
            new SubscriptionPeriod(1, SubscriptionPeriodUnit.Month));
        return new ProductCatalog("catalog-1", Now, [plan], [offer]);
    }

    private sealed class StubAccounts(AccountSession? session) : IAccountSessionProvider
    {
        public bool IsConfigured => true;
        public Task<AccountSession?> GetSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(session);
        public Task<AccountSession> SignInAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(session ?? new AccountSession("customer-1", "Reader", Now));
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("token");
    }

    private sealed class StubCommerceBackend : ICommerceBackend
    {
        public bool IsConfigured => true;
        public int CheckoutCount { get; private set; }
        public string? LastOfferId { get; private set; }
        public int RestoreCount { get; private set; }
        public bool FailRestore { get; set; }
        public Task? RestoreBarrier { get; set; }
        public int ConcurrentRestores { get; private set; }
        public int MaximumConcurrentRestores { get; private set; }
        public SubscriptionSummary? Subscription { get; set; } = new(
            new("free-period", "free", "Free", Now, Now.AddMonths(1)), [], Now);
        public Exception? SubscriptionFailure { get; set; }
        public Task<SubscriptionSummary?> GetSubscriptionAsync(CancellationToken cancellationToken = default) =>
            SubscriptionFailure is { } failure ? Task.FromException<SubscriptionSummary?>(failure) : Task.FromResult(Subscription);
        public Task<CheckoutSession> CreateCheckoutAsync(string offerId, CancellationToken cancellationToken = default)
        {
            CheckoutCount++;
            LastOfferId = offerId;
            if (Subscription is not null) Subscription = Subscription with {
                PendingCheckout = new("https://checkout.example.test/session", Now.AddMinutes(10)) };
            return Task.FromResult(new CheckoutSession(
                new Uri("https://checkout.example.test/session"), Now.AddMinutes(10)));
        }
        public async Task RestorePurchasesAsync(CancellationToken cancellationToken = default)
        {
            RestoreCount++;
            ConcurrentRestores++;
            MaximumConcurrentRestores = Math.Max(MaximumConcurrentRestores, ConcurrentRestores);
            try
            {
                if (FailRestore) throw new HttpRequestException("offline");
                if (RestoreBarrier is not null) await RestoreBarrier.WaitAsync(cancellationToken);
            }
            finally { ConcurrentRestores--; }
        }
    }

    private sealed class RecordingLauncher : IExternalUriLauncher
    {
        public Uri? LastUri { get; private set; }
        public void Open(Uri uri) => LastUri = uri;
    }

    private sealed class RecordingAccessService : IFeatureAccessService
    {
        public int InvalidateCount { get; private set; }
        public ValueTask<FeatureAccessDecision> GetAccessAsync(
            FeatureKey feature, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(FeatureAccessDecision.Denied(feature, FeatureAccessDenialReason.NotEntitled));
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Invalidate() => InvalidateCount++;
    }

    private sealed class FixedCatalogProvider(ProductCatalog catalog) : IProductCatalogProvider
    {
        public Task<ProductCatalog> GetCatalogAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(catalog);
    }

    private sealed class StaticHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class CallbackLauncher : IExternalUriLauncher
    {
        public Dictionary<string, string> Parameters { get; private set; } = [];

        public void Open(Uri uri)
        {
            Parameters = ParseQuery(uri.Query);

        }
    }

    private sealed class NoCallbackLauncher : IExternalUriLauncher
    {
        public TaskCompletionSource Opened { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Open(Uri uri) => Opened.TrySetResult();
    }

    private sealed class OAuthHandler : HttpMessageHandler
    {
        public Dictionary<string, string> TokenForm { get; private set; } = [];
        public string CodeVerifier => TokenForm["code_verifier"];
        public Dictionary<string, string> CreateForm { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/authorize")
            {
                CreateForm = ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Json("""{"request_secret":"sssssssssssssssssssssssssssssssssssssssssss","verification_uri":"https://identity.example.test/browser?request=browser-code","expires_in":900,"interval":3}""");
            }
            if (request.RequestUri?.AbsolutePath == "/authorize/token")
            {
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                TokenForm = ParseQuery(form);
                return Json("""
                    {"access_token":"access-token-secret","refresh_token":"refresh-token-secret","token_type":"Bearer","expires_in":3600}
                    """);
            }

            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-token-secret", request.Headers.Authorization?.Parameter);
            return Json("""{"subjectId":"customer-1","displayName":"Reader"}""");
        }

        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class CommerceHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public System.Net.Http.Headers.AuthenticationHeaderValue? CheckoutAuthorization { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string CheckoutBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri?.AbsolutePath == "/v1/checkout")
            {
                CheckoutAuthorization = request.Headers.Authorization;
                IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
                CheckoutBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return Json("""
                    {"checkoutUri":"https://checkout.example.test/session","expiresAtUtc":"2026-07-20T12:10:00Z"}
                    """);
            }

            return Json("{}");
        }

        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => Uri.UnescapeDataString(pair.Length > 1 ? pair[1].Replace('+', ' ') : string.Empty),
                StringComparer.Ordinal);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = Now;
        private readonly List<ClockTimer> _timers = [];
        public override DateTimeOffset GetUtcNow() => _now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ClockTimer(this, callback, state, dueTime);
            lock (_timers) _timers.Add(timer);
            return timer;
        }
        public void Advance(TimeSpan elapsed)
        {
            _now += elapsed;
            ClockTimer[] timers;
            lock (_timers) timers = _timers.ToArray();
            foreach (var timer in timers) timer.FireIfDue();
        }
        private sealed class ClockTimer(ManualClock clock, TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
        {
            private DateTimeOffset? _due = clock._now + dueTime;
            public bool Change(TimeSpan dueTime, TimeSpan period) { _due = clock._now + dueTime; return true; }
            public void Dispose() => _due = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
            public void FireIfDue()
            {
                if (_due is { } due && due <= clock._now) { _due = null; callback(state); }
            }
        }
    }
}
