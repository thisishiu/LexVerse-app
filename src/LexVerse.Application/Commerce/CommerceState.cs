using LexVerse.Application.Account;
using LexVerse.Core.Product;

namespace LexVerse.Application.Commerce;

public enum CommerceStatus
{
    Unconfigured,
    SignedOut,
    Loading,
    Ready,
    CheckoutOpened,
    Error
}

public sealed record CommerceState(
    CommerceStatus Status,
    AccountSession? Account = null,
    ProductCatalog? Catalog = null,
    SubscriptionSummary? Subscription = null,
    DateTimeOffset? LastSyncedAtUtc = null,
    CheckoutSession? PendingCheckout = null,
    bool CatalogError = false,
    bool SyncError = false,
    string? SubscriptionError = null);

/// <summary>A safe error code for subscription synchronization; contains no remote payload or credentials.</summary>
public sealed class SubscriptionSyncException(string code, Exception? innerException = null)
    : Exception(code, innerException)
{
    public string Code { get; } = code;
}

public sealed record SubscriptionPeriodSummary(string Id, string PlanId, string DisplayName,
    DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

public sealed record SubscriptionSummary(SubscriptionPeriodSummary Current,
    IReadOnlyList<SubscriptionPeriodSummary> Upcoming, DateTimeOffset SyncedAtUtc,
    PendingCheckoutSummary? PendingCheckout = null);

public sealed record PendingCheckoutSummary(string CheckoutUri, DateTimeOffset ExpiresAtUtc);
