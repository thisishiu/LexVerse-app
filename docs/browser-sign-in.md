# Browser sign-in

The desktop app uses the system browser, PKCE S256 and a temporary loopback listener. Tokens remain in the DPAPI account store; authorization URLs are only exposed to the native clipboard command, never in snapshots.

`account.signIn` acknowledges the start immediately. Snapshots publish `commerce.authentication` with `status` (`idle`, `opening`, `waiting`, `completing`, `success`, `error`) and an optional sanitized `error` code. `account.reopenSignIn`, `account.copySignInLink` and `account.cancelSignIn` recover or cancel the same pending attempt. Repeated starts do not create competing attempts.

The app and server wait up to 15 minutes. Registration creates an unverified browser session; email verification still requires a POST. Verified users automatically return through `/oauth/continue`. The waiting tab polls even in the background, including when verification occurs in a different browser. Browser timer throttling can delay that check.

Deploy the server first, then distribute the app. The server retains POST `/oauth/continue` for already-open consent forms. No account migration is needed.

## Validation

- Server: `rtk npm test` in `lexverse-server`.
- Provider and coordinator: `rtk dotnet test tests/LexVerse.Translation.Tests --no-restore`.
- UI: `rtk npm run check`. If Windows fork workers cannot start, use `rtk proxy npx vitest run --pool=threads --maxWorkers=1` after the TypeScript check.
- App: `rtk dotnet build src/LexVerse.App --no-restore`. Use an isolated `OutDir` if a running app locks the default output.

## Installed Windows acceptance

These checks require the new server and the newly installed app; automated loopback tests do not replace them.

1. Sign in with and without an existing browser session; verify that the app updates without another connection button.
2. Register and verify email in the same browser, then in a different browser with the original waiting tab in the background. No second password entry should be needed.
3. Wait over three minutes before completion; retry after expiry at fifteen minutes.
4. Exercise reopen, copy-link, cancellation, and repeated clicks. Cancel and start again; an old tab must not sign in the new attempt.
5. Check browser-launch failure and network failure recovery. The browser must not announce success unless the app has saved the session.
6. Check that unavailable plan information does not discard a successful sign-in.
