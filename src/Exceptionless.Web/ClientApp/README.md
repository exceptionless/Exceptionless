# Exceptionless User Interface

This is the only Exceptionless application frontend: Svelte 5 and SvelteKit, served at `/`. New links use root routes; primary historical `#!/` and `#/` links remain supported by the navigation compatibility layer. The existing session storage key is retained so users stay signed in.

## Developing

For full-stack development, run `aspire run` from the repository root. Open the `App` endpoint reported by Aspire, normally `https://web-ex.dev.localhost:7131/`. Worktrees may use dynamic ports. Aspire supplies this frontend address to both the API and background jobs for OAuth and email links. Vite proxies API requests and WebSockets to the API resource.

For frontend commands, run from this directory and install dependencies with `npm ci`:

```powershell
npm ci
npm run dev
```

The standalone dev server needs a running API. `npm run urls` reports local Aspire endpoints; set `API_HTTPS` or `API_HTTP` to the reported API URL when starting Vite outside Aspire. Follow [AGENTS.md](AGENTS.md) for frontend conventions and verification scope.

## Building

To create a production version of your app:

```powershell
npm run build
```

The static output goes to `build/`. Publishing `Exceptionless.Web` includes it directly in `wwwroot`; Docker app images use `build/update-config.sh` from the repository root to write public runtime configuration. Startup stops if configuration generation fails. `EX_ApiUrl` selects a separate API origin for requests and the API Reference link when configured, otherwise they use the app origin.

`EX_EnableSsl=true` retains forced HTTPS navigation for self-hosted deployments, including proxies that forward to an HTTP-only app container. The UI upgrades the current URL before starting the router or authentication, preserving its path, query, fragment, and any explicit nonstandard port. The default remains `false`. Standalone Vite development uses `PUBLIC_ENABLE_SSL` for the same behavior.

Set the backend's `EX_BaseURL` to the public UI URL. Email links and OAuth authorization redirects use this address, including when the API has a separate origin.

New email, Slack, and webhook links use the root application origin even if `EX_BaseURL` still contains a historical hash suffix. Incoming compatibility links also normalize existing browser history entries so Back and Forward continue to work.

For separate UI and API hosts, set `EX_ApiUrl` to the public API URL on both deployments. The API uses this origin for OAuth discovery, issuer, resource validation, and authentication challenges; the browser authorization page still uses `EX_BaseURL`. If `EX_ApiUrl` is unset, the API retains `EX_BaseURL` as its canonical origin.

`npm run preview` previews the static build only; use Aspire for full-stack testing.

## Testing

```powershell
npm run test:unit -- --run
npm run test:e2e
```

See the [E2E guide](e2e/README.md) for local runtime setup and compatibility coverage. Before pushing frontend changes, run `npm run validate` and inspect formatting changes.

## Upgrading components

You can upgrade [shadcn-svelte components](https://www.shadcn-svelte.com/) by running the following command

```bash
npx shadcn-svelte@latest update
```
