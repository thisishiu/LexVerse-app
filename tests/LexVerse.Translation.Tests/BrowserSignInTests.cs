using System.Net;
using System.Net.Sockets;
using LexVerse.Application.Account;
using LexVerse.Application.Commerce;
using LexVerse.Application.Product;
using LexVerse.Core.Product;
using LexVerse.Infrastructure.Commerce;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class BrowserSignInTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("token")]
    [InlineData("profile")]
    [InlineData("store")]
    [InlineData("timeout")]
    public async Task ManualLinkSurvivesLauncherFailureAndUnrelatedCallbacks(string? failure)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-browser-{Guid.NewGuid():N}");
        try
        {
            var store = new WindowsDpapiAccountStore(Path.Combine(directory, "session.dat"));
            if (failure == "store") Directory.CreateDirectory(Path.Combine(directory, "session.dat"));
            using var http = new HttpClient(new IdentityHandler(failure));
            var launcher = new BrokenLauncher();
            using var provider = new OAuthPkceAccountSessionProvider(http, Options(), store, launcher);
            var pending = provider.SignInAsync(deadline.Token);
            var authorization = provider.PendingAuthorizationUri!;
            Assert.Equal("browser-open-failed", provider.SignInState.Error);
            provider.ReopenBrowser();
            Assert.Equal(authorization, provider.PendingAuthorizationUri);
            Assert.Equal(2, launcher.Calls);
            var parameters = Query(authorization);
            var callback = parameters["redirect_uri"];
            using var browser = new HttpClient();
            // A probe and an error callback without the correct state cannot end sign-in.
            try { using var probe = await browser.GetAsync(new Uri(new Uri(callback), "/favicon.ico"), deadline.Token); }
            catch (HttpRequestException) { }
            using var wrongState = await browser.GetAsync(callback + "?error=access_denied&state=wrong", deadline.Token);
            Assert.Equal(HttpStatusCode.BadRequest, wrongState.StatusCode);
            Assert.False(pending.IsCompleted);
            using var response = await browser.GetAsync(callback + "?code=test-code&state=" + parameters["state"], deadline.Token);
            var page = await response.Content.ReadAsStringAsync(deadline.Token);
            if (failure is not null)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => pending);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.DoesNotContain("Đã đăng nhập LexVerse", page);
                Assert.Null(await provider.GetSessionAsync(deadline.Token));
                Assert.Equal("error", provider.SignInState.Status);
            }
            else
            {
                Assert.Equal("reader", (await pending).SubjectId);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("Đã đăng nhập LexVerse", page);
                Assert.NotNull(await provider.GetSessionAsync(deadline.Token));
                Assert.Equal("success", provider.SignInState.Status);
            }
            Assert.Null(provider.PendingAuthorizationUri);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task StalledLocalConnectionDoesNotConsumeTheValidCallback()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-stalled-{Guid.NewGuid():N}");
        try
        {
            using var http = new HttpClient(new IdentityHandler(null));
            using var provider = new OAuthPkceAccountSessionProvider(http, Options(),
                new WindowsDpapiAccountStore(Path.Combine(directory, "session.dat")), new BrokenLauncher());
            var pending = provider.SignInAsync(deadline.Token);
            var parameters = Query(provider.PendingAuthorizationUri!);
            var callback = new Uri(parameters["redirect_uri"]);
            using var stalled = new TcpClient();
            await stalled.ConnectAsync(callback.Host, callback.Port, deadline.Token);
            using var browser = new HttpClient();
            using var response = await browser.GetAsync(callback + "?code=test-code&state=" + parameters["state"], deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("reader", (await pending).SubjectId);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CancelClosesAttemptAndAllowsRetry()
    {
        using var http = new HttpClient(new IdentityHandler(null));
        using var provider = new OAuthPkceAccountSessionProvider(http, Options(),
            new WindowsDpapiAccountStore(Path.Combine(Path.GetTempPath(), $"unused-{Guid.NewGuid():N}.dat")), new BrokenLauncher());
        var first = provider.SignInAsync(TestContext.Current.CancellationToken);
        var firstUri = provider.PendingAuthorizationUri;
        provider.CancelSignIn();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Null(provider.PendingAuthorizationUri);
        var second = provider.SignInAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(firstUri, provider.PendingAuthorizationUri);
        provider.CancelSignIn();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }

    [Fact]
    public async Task PendingSignInSurvivesThreeMinutesAndExpiresAtFifteen()
    {
        var clock = new ManualClock();
        using var http = new HttpClient(new IdentityHandler(null));
        using var provider = new OAuthPkceAccountSessionProvider(http, Options(),
            new WindowsDpapiAccountStore(Path.Combine(Path.GetTempPath(), $"unused-{Guid.NewGuid():N}.dat")), new BrokenLauncher(), clock);
        var pending = provider.SignInAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.False(pending.IsCompleted);
        Assert.NotNull(provider.PendingAuthorizationUri);
        clock.Advance(TimeSpan.FromMinutes(11));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(provider.PendingAuthorizationUri);
        Assert.Equal("error", provider.SignInState.Status);
    }

    [Fact]
    public async Task RepeatedBeginDoesNotCreateCompetingAttemptsAndCancelDoesNotBlockBridge()
    {
        var accounts = new Accounts { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var coordinator = new CommerceCoordinator(accounts, new BrokenCatalog(), new Backend(), new BrokenLauncher(), new Access());
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.StateChanged += (_, _) =>
        {
            if (coordinator.Authentication.Status == "error") finished.TrySetResult();
        };
        coordinator.BeginSignIn();
        coordinator.BeginSignIn();
        Assert.Equal(1, accounts.Calls);
        coordinator.CancelSignIn();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, accounts.Calls);
    }

    [Fact]
    public async Task CatalogFailureDoesNotDiscardSignedInAccount()
    {
        using var coordinator = new CommerceCoordinator(new Accounts(), new BrokenCatalog(), new Backend(), new BrokenLauncher(), new Access());
        await coordinator.SignInAsync(TestContext.Current.CancellationToken);
        Assert.Equal(CommerceStatus.Ready, coordinator.State.Status);
        Assert.Equal("reader", coordinator.State.Account?.SubjectId);
    }

    private static OAuthNativeAppOptions Options() => new(new Uri("https://identity.example.test/authorize"),
        new Uri("https://identity.example.test/token"), new Uri("https://identity.example.test/me"), "lexverse-desktop");
    private static Dictionary<string, string> Query(Uri uri) => uri.Query.TrimStart('?').Split('&')
        .Select(pair => pair.Split('=', 2)).ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));
    private sealed class BrokenLauncher : IExternalUriLauncher
    {
        public int Calls { get; private set; }
        public void Open(Uri uri) { Calls++; throw new InvalidOperationException("No browser"); }
    }
    private sealed class IdentityHandler(string? failure) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (failure == "timeout")
            {
                await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken);
                throw new OperationCanceledException("Simulated HTTP timeout");
            }
            return new HttpResponseMessage((failure == "token" && request.RequestUri!.AbsolutePath == "/token") ||
                (failure == "profile" && request.RequestUri!.AbsolutePath == "/me") ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath == "/token"
                    ? """{"access_token":"secret","refresh_token":"refresh","token_type":"Bearer","expires_in":3600}"""
                    : """{"subjectId":"reader","displayName":"Reader"}""")
            };
        }
    }
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        private readonly List<ClockTimer> _timers = [];
        public override DateTimeOffset GetUtcNow() => _now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ClockTimer(this, callback, state, dueTime);
            _timers.Add(timer);
            return timer;
        }
        public void Advance(TimeSpan elapsed)
        {
            _now += elapsed;
            foreach (var timer in _timers.ToArray()) timer.FireIfDue();
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
    private sealed class Accounts : IAccountSessionProvider
    {
        public TaskCompletionSource<AccountSession>? Pending { get; init; }
        public int Calls { get; private set; }
        public bool IsConfigured => true;
        public Task<AccountSession?> GetSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult<AccountSession?>(null);
        public Task<AccountSession> SignInAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Pending?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(new AccountSession("reader", "Reader", DateTimeOffset.UtcNow));
        }
        public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("secret");
    }
    private sealed class BrokenCatalog : IProductCatalogProvider
    {
        public Task<ProductCatalog> GetCatalogAsync(CancellationToken cancellationToken = default) => throw new HttpRequestException();
    }
    private sealed class Backend : ICommerceBackend
    {
        public bool IsConfigured => true;
        public Task<CheckoutSession> CreateCheckoutAsync(string offerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RestorePurchasesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Access : IFeatureAccessService
    {
        public ValueTask<FeatureAccessDecision> GetAccessAsync(FeatureKey feature, CancellationToken cancellationToken = default) => ValueTask.FromResult(FeatureAccessDecision.Denied(feature, FeatureAccessDenialReason.NotEntitled));
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Invalidate() { }
    }
}
