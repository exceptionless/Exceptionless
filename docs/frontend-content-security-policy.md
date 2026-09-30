# Modern frontend content security policy

This policy targets the Svelte 5/SvelteKit static application mounted at `/next`.
It assumes the legacy Angular frontend is retired. This PR does not retire that
application, alter routing, or change deployment. Its Angular subtree matches
`main`; Angular-specific nonce middleware, renderer changes, and tests are not
part of this PR.

ASP.NET serves the built SPA and stamps every HTML script with a fresh 32-byte
response nonce. The Svelte server hook protects development HTML; static builds
receive their policy from ASP.NET. Both generators are checked against
`src/Exceptionless.Web/Security/frontend-content-security-policy.contract.json`.
HTML cannot be cached or served partially with a mismatched nonce. API and MCP
streaming responses bypass HTML transformation.

## Sources and consumers

| Directive | Consumer and retained sources |
| --- | --- |
| `default-src` | Same-origin assets and API calls. |
| `script-src` | Nonce plus `strict-dynamic`; same-origin, Stripe.js (`js.stripe.com`, `*.js.stripe.com`, `maps.googleapis.com`) and Intercom script hosts are CSP2 compatibility sources. No `unsafe-inline` or `unsafe-eval`. Zod JIT is disabled. |
| `connect-src` | Same-origin API, streaming assistant responses, uploads, health checks, and `/api/v2/push` WebSocket; Exceptionless collector/config/heartbeat telemetry; Stripe API, Maps and Link; US Intercom API, realtime and upload endpoints. Includes exactly `https://*.intercom-messenger.com` and `wss://*.intercom-messenger.com`. |
| `style-src` | Same-origin CSS; inline styles used by Svelte components and Intercom; jsDelivr for Scalar API documentation served by the same ASP.NET host. |
| `font-src` | Same-origin fonts, Intercom font CDNs, and Scalar's jsDelivr assets. Google Fonts are not used by the modern app. |
| `img-src` | Same-origin uploaded avatars, Gravatar fallback, image previews (`blob:`/`data:`), Stripe/Link and documented US Intercom images/attachments. No direct GitHub avatar host is needed: `UserHandler` serves uploaded avatars through `/api/v2/users/...`. |
| `frame-src` | Same-origin; Stripe.js, authentication/3DS and Link frames; Intercom article, reporting and embedded-video frames. |
| `worker-src` | Same-origin, blob workers, and Intercom's documented child sources. Kept separate from `frame-src` following Intercom's CSP3 guidance. |
| `media-src` | Same-origin, blob media, Intercom media/download CDNs. |
| `form-action` | Same-origin plus Intercom help and US API forms. OAuth uses top-level browser navigation, not cross-origin form submission or fetch. |
| `manifest-src` | Same-origin. |
| `base-uri`, `object-src`, `frame-ancestors` | `none`: no base override, plugins, or embedding of this application. |

US Intercom endpoints are retained without EU/Australia blanket allowances.
Google Maps remains because Stripe's Stripe.js CSP guide lists it, even though
the Svelte application itself does not render Google Maps. Stripe Checkout,
Connect embedded components, crypto onramp, OAuth fetch hosts, and arbitrary
third-party CDNs are not enabled merely because the provider offers them.

Svelte development alone adds `ws:` and `wss:` for Vite HMR, including forwarded
Codespaces hosts. Production policies do not include those scheme-wide sources.
The application uses a real WebSocket, not EventSource: the authenticated layout
creates `WebSocketClient`, which opens `/api/v2/push` using `window.location`.
Because some browsers do not match WebSocket schemes against `'self'`, production
also allows one exact WebSocket origin derived from administrator configuration:
`BaseURL` for ASP.NET, `PUBLIC_BASE_URL` for the Svelte server hook. HTTPS becomes
WSS and HTTP becomes WS, preserving non-default ports and IPv6 while discarding
paths, queries and fragments. Request `Host` and forwarded headers never supply
this source.

These configured URLs must match the externally served application origin,
including the public port, in reverse-proxy and self-hosted deployments. ASP.NET
already requires `BaseURL`; invalid HTTP(S) origin configuration now fails
explicitly rather than widening the policy. A missing or empty `PUBLIC_BASE_URL` leaves
the Svelte hook restrictive (`'self'` and provider sources only); it never enables
production `ws:`/`wss:`. If a public URL is supplied but invalid, generation fails
explicitly. Static Svelte builds still use the ASP.NET runtime configuration.
No new API/WebSocket configuration keys are introduced. Custom external telemetry
endpoints still require a deliberate policy update.

## Validation and limits

Backend HTTP tests cover `/next` index/fallback HTML, nonce rotation, static/API
bypass, conditional/range handling, configured WebSocket origins, poisoned
Host/forwarded headers, and Scalar HTML. Svelte tests cover policy
parity, both Intercom messenger schemes, obsolete-source exclusion, nonce
injection and production/development connection differences. Run these tests
alongside the production build and lint checks when changing sources.

External payment, OAuth and authenticated Intercom behavior requires a configured
staging environment; local policy/header tests do not certify those provider
flows. Do not solve an observed violation by adding unrestricted schemes or
relaxing script restrictions. Confirm the actual consumer and destination first.

Provider references (reviewed September 30, 2026):

- [Stripe integration security guide](https://docs.stripe.com/security/guide)
- [Intercom CSP requirements](https://www.intercom.com/help/en/articles/3894-using-intercom-with-content-security-policy)
- [MDN connect-src and WebSocket scheme compatibility](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src)
