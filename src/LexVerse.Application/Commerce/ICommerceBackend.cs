namespace LexVerse.Application.Commerce;

public interface ICommerceBackend
{
    bool IsConfigured { get; }

    Task<CheckoutSession> CreateCheckoutAsync(
        string offerId,
        CancellationToken cancellationToken = default);

    Task RestorePurchasesAsync(CancellationToken cancellationToken = default);

    Task<SubscriptionSummary?> GetSubscriptionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<SubscriptionSummary?>(null);
}
