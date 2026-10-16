# Browser sign-in

The desktop creates a 15-minute HTTPS sign-in request with PKCE S256, opens its verification URL, and polls for tokens every three seconds. No loopback listener, callback URL, or custom URI registration is used. The browser requires explicit account confirmation and finishes on the server's success page.

Configure `LEXVERSE_OAUTH_DESKTOP_REQUEST_ENDPOINT=https://lexverse.world/oauth/desktop/requests`. Keep the existing `/oauth/token` refresh endpoint, account endpoint and client ID. DPAPI storage and token refresh are unchanged.

Opening the browser again reuses the same request. Cancel stops polling locally; the server record expires automatically. Network failures and HTTP timeouts retry with increased delay; `slow_down` adds five seconds, up to 60 seconds. Invalid or expired requests require a new sign-in.

Deploy the server first, then release the new app immediately. Old `/oauth/authorize` and authorization-code exchange return an update-required response. Existing refresh tokens remain valid. Sessions already waiting on the old callback must restart in the updated app.

## Browser acceptance

Use the system Chrome browser against the new server: sign in, verify email if needed, confirm the displayed account once, and see the success page. Check that the app receives the account, no navigation targets localhost, and Console contains no form-action error. Repeat with wrong password, reopening the browser, cancellation, expired requests, and account switching by signing out of the website first.
