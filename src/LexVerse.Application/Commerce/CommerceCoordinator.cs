using LexVerse.Application.Account;
using LexVerse.Application.Product;
using LexVerse.Core.Product;

namespace LexVerse.Application.Commerce;

/// <summary>Serializes account and purchase operations while keeping backend authority intact.</summary>
public sealed class CommerceCoordinator : IDisposable
{
    private readonly IAccountSessionProvider _accounts;
    private readonly IProductCatalogProvider _catalogs;
    private readonly ICommerceBackend _commerce;
    private readonly IExternalUriLauncher _launcher;
    private readonly IFeatureAccessService _featureAccess;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private readonly TimeProvider _timeProvider;
    private Task? _syncLoop;
    private CancellationTokenSource? _syncCancellation;

    public CommerceCoordinator(
        IAccountSessionProvider accounts,
        IProductCatalogProvider catalogs,
        ICommerceBackend commerce,
        IExternalUriLauncher launcher,
        IFeatureAccessService featureAccess,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(commerce);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(featureAccess);
        _accounts = accounts;
        _catalogs = catalogs;
        _commerce = commerce;
        _launcher = launcher;
        _featureAccess = featureAccess;
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (_accounts is IBrowserSignIn browser)
            browser.SignInStateChanged += BrowserSignInStateChanged;
        State = new CommerceState(
            accounts.IsConfigured && commerce.IsConfigured
                ? CommerceStatus.Loading
                : CommerceStatus.Unconfigured);
    }

    public CommerceState State { get; private set; }

    public event EventHandler<CommerceState>? StateChanged;
    public BrowserSignInState Authentication { get; private set; } = new();
    public Uri? PendingAuthorizationUri => (_accounts as IBrowserSignIn)?.PendingAuthorizationUri;
    public void ReopenSignIn() => (_accounts as IBrowserSignIn)?.ReopenBrowser();
    public void CancelSignIn()
    {
        _browserSignIn?.Cancel();
        (_accounts as IBrowserSignIn)?.CancelSignIn();
    }
    private void BrowserSignInStateChanged(object? sender, BrowserSignInState state)
    {
        Authentication = state;
        StateChanged?.Invoke(this, State);
    }
    private Task? _pendingSignIn;
    private CancellationTokenSource? _browserSignIn;
    public void BeginSignIn()
    {
        if (_pendingSignIn is { IsCompleted: false }) return;
        BrowserSignInStateChanged(this, new("opening"));
        _browserSignIn = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _pendingSignIn = ObserveSignInAsync(_browserSignIn);
    }
    private async Task ObserveSignInAsync(CancellationTokenSource attempt)
    {
        try { await SignInAsync(attempt.Token); }
        catch (Exception)
        {
            if (Authentication.Status != "error")
                BrowserSignInStateChanged(this, new("error", "sign-in-failed"));
        }
        finally
        {
            _browserSignIn = null;
            attempt.Dispose();
        }
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async token =>
        {
            var session = await _accounts.GetSessionAsync(token);
            ProductCatalog? catalog = null;
            var catalogError = false;
            try { catalog = await _catalogs.GetCatalogAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { catalogError = true; }
            if (session is null)
            {
                SetState(new CommerceState(CommerceStatus.SignedOut, Catalog: catalog, CatalogError: catalogError));
                return;
            }

            SetState(new CommerceState(CommerceStatus.Ready, session, catalog, CatalogError: catalogError));
            StartSyncLoop();
            await RefreshAccountAsync(token);
        }, cancellationToken);

    public Task SignInAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async token =>
        {
            var session = await _accounts.SignInAsync(token);
            SetState(State with { Status = CommerceStatus.Ready, Account = session });
            var catalog = State.Catalog;
            try { catalog = await _catalogs.GetCatalogAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { /* Catalog availability must not discard the authenticated session. */ }
            SetState(new CommerceState(CommerceStatus.Ready, session, catalog));
            StartSyncLoop();
            await RefreshAccountAsync(token);
        }, cancellationToken);

    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async token =>
        {
            try
            {
                await _accounts.SignOutAsync(token);
            }
            finally
            {
                _featureAccess.Invalidate();
            }

            SetState(State with { Status = CommerceStatus.SignedOut, Account = null,
                Subscription = null, PendingCheckout = null, LastSyncedAtUtc = null,
                SubscriptionError = null, SyncError = false });
            var syncCancellation = _syncCancellation;
            _syncCancellation = null;
            syncCancellation?.Cancel();
            _syncLoop = null;
        }, cancellationToken);

    public Task StartCheckoutAsync(string offerId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async token =>
        {
            var current = State;
            if (current.Account is null || current.Catalog?.FindOffer(offerId) is not { IsPublic: true })
            {
                throw new InvalidOperationException("A current public offer and signed-in account are required.");
            }

            var checkout = await _commerce.CreateCheckoutAsync(offerId, token);
            _launcher.Open(checkout.CheckoutUri);
            SetState(current with { Status = CommerceStatus.CheckoutOpened, PendingCheckout = checkout });
            StartSyncLoop();
        }, cancellationToken);

    public Task RestorePurchasesAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async token =>
        {
            if (State.Account is null)
            {
                var catalog = await _catalogs.GetCatalogAsync(token);
                SetState(State with { Status = CommerceStatus.SignedOut, Catalog = catalog, CatalogError = false });
                return;
            }

            await RefreshAccountAsync(token);
        }, cancellationToken);

    private async Task RefreshAccountAsync(CancellationToken token)
    {
        var syncError = false;
        try { await _commerce.RestorePurchasesAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { syncError = true; }
        SubscriptionSummary? subscription = null;
        SubscriptionSyncException? subscriptionError = null;
        try
        {
            subscription = await _commerce.GetSubscriptionAsync(token)
                ?? throw new SubscriptionSyncException("subscription-invalid-response");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            subscriptionError = exception as SubscriptionSyncException
                ?? new SubscriptionSyncException("subscription-unavailable", exception);
        }
        await TryRefreshFeatureAccessAsync(token);
        var pending = State.PendingCheckout;
        if (subscription is not null)
            pending = subscription.PendingCheckout is { } remote
                ? new CheckoutSession(new Uri(remote.CheckoutUri), remote.ExpiresAtUtc,
                    new Uri(remote.CheckoutUri).IsLoopback)
                : null;
        if (pending?.ExpiresAtUtc <= _timeProvider.GetUtcNow()) pending = null;
        ProductCatalog? catalog = State.Catalog;
        var catalogError = false;
        try { catalog = await _catalogs.GetCatalogAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { catalogError = true; }
        SetState(State with { Status = pending is null ? CommerceStatus.Ready : CommerceStatus.CheckoutOpened,
            Subscription = subscription ?? State.Subscription, PendingCheckout = pending, Catalog = catalog,
            CatalogError = catalogError, SyncError = syncError, SubscriptionError = subscriptionError?.Code,
            LastSyncedAtUtc = subscription is null ? State.LastSyncedAtUtc : _timeProvider.GetUtcNow() });
        if (subscriptionError is not null) throw subscriptionError;
    }

    private void StartSyncLoop()
    {
        if (_syncLoop is { IsCompleted: false }) return;
        _syncCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _syncLoop = SyncLoopAsync(_syncCancellation);
    }

    private async Task SyncLoopAsync(CancellationTokenSource cancellation)
    {
        var syncToken = cancellation.Token;
        var lastAttempt = _timeProvider.GetUtcNow();
        try
        {
            // A short heartbeat also notices a newly opened checkout without replacing timers.
            while (!syncToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), _timeProvider, syncToken);
                if (State.Account is null) continue;
                if (!await _gate.WaitAsync(0, syncToken)) continue;
                try
                {
                    if (State.Account is null) continue;
                    if (State.PendingCheckout?.ExpiresAtUtc <= _timeProvider.GetUtcNow())
                        SetState(State with { PendingCheckout = null, Status = CommerceStatus.Ready });
                    var interval = State.PendingCheckout is null ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10);
                    if (_timeProvider.GetUtcNow() - lastAttempt < interval) continue;
                    lastAttempt = _timeProvider.GetUtcNow();
                    SetState(State with { Status = CommerceStatus.Loading });
                    await RefreshAccountAsync(syncToken);
                }
                catch (OperationCanceledException) when (syncToken.IsCancellationRequested) { break; }
                catch (SubscriptionSyncException) { SetState(State with { Status = CommerceStatus.Error }); }
                catch { SetState(State with { SyncError = true }); }
                finally { _gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (syncToken.IsCancellationRequested) { }
        finally { cancellation.Dispose(); }
    }

    private async Task TryRefreshFeatureAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _featureAccess.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _featureAccess.Invalidate();
        }
    }

    private async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_accounts.IsConfigured || !_commerce.IsConfigured)
        {
            SetState(new CommerceState(CommerceStatus.Unconfigured));
            return;
        }

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetime.Token);
        cancellationToken = lifetime.Token;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            SetState(State with { Status = CommerceStatus.Loading });
            await operation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            SetState(State with { Status = CommerceStatus.Error });
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void SetState(CommerceState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_accounts is IBrowserSignIn browser)
            browser.SignInStateChanged -= BrowserSignInStateChanged;
        _lifetime.Cancel();
    }
}
