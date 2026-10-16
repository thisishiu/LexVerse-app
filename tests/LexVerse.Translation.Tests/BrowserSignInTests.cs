using System.Net;
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
    public async Task HttpsPollingPreservesManualLinkAndPersistsOnlyOnSuccess(string? failure)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-browser-{Guid.NewGuid():N}");
        try
        {
            var store = new WindowsDpapiAccountStore(Path.Combine(directory, "session.dat"));
            if (failure == "store") Directory.CreateDirectory(Path.Combine(directory, "session.dat"));
            using var http = new HttpClient(new IdentityHandler(failure));
            var launcher = new BrokenLauncher();
            using var provider = new OAuthPkceAccountSessionProvider(http, Options(), store, launcher);
            var pending = provider.SignInAsync(TestContext.Current.CancellationToken);
            var authorization = provider.PendingAuthorizationUri!;
            Assert.Equal("https", authorization.Scheme);
            Assert.DoesNotContain("redirect_uri", authorization.Query);
            Assert.Equal("browser-open-failed", provider.SignInState.Error);
            provider.ReopenBrowser();
            Assert.Equal(authorization, provider.PendingAuthorizationUri);
            Assert.Equal(2, launcher.Calls);
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.SignInAsync(TestContext.Current.CancellationToken));
            if (failure is not null)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => pending);
                Assert.Null(await provider.GetSessionAsync(TestContext.Current.CancellationToken));
                Assert.Equal("error", provider.SignInState.Status);
            }
            else
            {
                Assert.Equal("reader", (await pending).SubjectId);
                Assert.NotNull(await provider.GetSessionAsync(TestContext.Current.CancellationToken));
                Assert.Equal("success", provider.SignInState.Status);
            }
            Assert.Null(provider.PendingAuthorizationUri);
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
        using var http = new HttpClient(new IdentityHandler("pending"));
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

    [Theory]
    [InlineData("slow_down")]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("pending")]
    [InlineData("expired_token")]
    public async Task PollingBacksOffOrStopsWithoutLosingTheBrowserRequest(string firstResult)
    {
        var clock = new ManualClock();
        var handler = new PollHandler(firstResult);
        var directory = Path.Combine(Path.GetTempPath(), $"lexverse-poll-{Guid.NewGuid():N}");
        try
        {
            using var http = new HttpClient(handler);
            using var provider = new OAuthPkceAccountSessionProvider(http, Options(),
                new WindowsDpapiAccountStore(Path.Combine(directory, "session.dat")), new BrokenLauncher(), clock);
            var pending = provider.SignInAsync(TestContext.Current.CancellationToken);
            var uri = provider.PendingAuthorizationUri;
            clock.Advance(TimeSpan.FromSeconds(3));
            await WaitUntilAsync(() => handler.Polls == 1);
            if (firstResult == "expired_token")
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
                Assert.Null(provider.PendingAuthorizationUri);
                return;
            }
            // Give the poll continuation time to install the next timer.
            await Task.Delay(50, TestContext.Current.CancellationToken);
            Assert.Equal(uri, provider.PendingAuthorizationUri);
            if (firstResult != "pending")
            {
                clock.Advance(TimeSpan.FromSeconds(7));
                Assert.Equal(1, handler.Polls);
                clock.Advance(TimeSpan.FromSeconds(1));
            }
            else clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal("reader", (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).SubjectId);
            Assert.Equal(2, handler.Polls);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.True(condition());
    }

    private sealed class PollHandler(string firstResult) : HttpMessageHandler
    {
        public int Polls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath != "/authorize/token")
                return await new IdentityHandler(null).RespondAsync(request, cancellationToken);
            if (Interlocked.Increment(ref Polls) == 1)
            {
                if (firstResult == "network") throw new HttpRequestException("Network unavailable");
                if (firstResult == "timeout")
                {
                    // Simulate an HTTP transport timeout without cancelling sign-in.
                    throw new TaskCanceledException("HTTP timeout");
                }
                return new(HttpStatusCode.BadRequest) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new {
                    error = firstResult == "pending" ? "authorization_pending" : firstResult })) };
            }
            return await new IdentityHandler(null).RespondAsync(request, cancellationToken);
        }
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
    private sealed class BrokenLauncher : IExternalUriLauncher
    {
        public int Calls { get; private set; }
        public void Open(Uri uri) { Calls++; throw new InvalidOperationException("No browser"); }
    }
    private sealed class IdentityHandler(string? failure) : HttpMessageHandler
    {
        public Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken cancellationToken) => SendAsync(request, cancellationToken);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            if (request.RequestUri!.AbsolutePath == "/authorize")
                return new(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new {
                    request_secret = new string('s', 43), verification_uri = "https://identity.example.test/browser?request=" + Guid.NewGuid(),
                    expires_in = 900, interval = 3 })) };
            if (failure == "pending" && request.RequestUri.AbsolutePath == "/authorize/token")
                return new(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"authorization_pending"}""") };
            return new HttpResponseMessage((failure == "token" && request.RequestUri.AbsolutePath == "/authorize/token") ||
                (failure == "profile" && request.RequestUri.AbsolutePath == "/me") ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            {
                Content = new StringContent(failure == "token" ? """{"error":"invalid_grant"}""" : request.RequestUri.AbsolutePath == "/authorize/token"
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
