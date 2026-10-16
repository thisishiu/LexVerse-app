namespace LexVerse.Application.Account;

public sealed record BrowserSignInState(string Status = "idle", string? Error = null);

public interface IBrowserSignIn
{
    BrowserSignInState SignInState { get; }
    event EventHandler<BrowserSignInState>? SignInStateChanged;
    Uri? PendingAuthorizationUri { get; }
    void ReopenBrowser();
    void CancelSignIn();
}
