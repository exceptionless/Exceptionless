# Exceptionless User Interface

This is the only Exceptionless application frontend: Svelte 5 and SvelteKit, served at `/`. The Angular application and its build tooling have been removed. New links use root routes; primary historical `/next`, `#!/`, and `#/` links remain supported by the navigation compatibility layer. The existing session storage key is retained so users stay signed in.

## Developing

For full-stack development, run `aspire run` from the repository root. Open the `App` endpoint reported by Aspire, normally `https://web-ex.dev.localhost:7131/`. Worktrees may use dynamic ports. Vite proxies API requests and WebSockets to the API resource.

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

The static output goes to `build/`. Publishing `Exceptionless.Web` includes it directly in `wwwroot`; Docker app images use `build/update-config.sh` from the repository root to write public runtime configuration. `EX_ApiUrl` selects a separate API origin when configured, otherwise requests use the app origin.

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
