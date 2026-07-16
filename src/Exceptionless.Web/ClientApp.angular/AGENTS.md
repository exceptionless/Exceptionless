# Legacy Angular ClientApp

This directory is the legacy AngularJS frontend. Treat it as maintenance-only.

Only edit this app when the user explicitly asks for Angular/legacy UI work or the bug exists only in the legacy UI. New frontend development belongs in `../ClientApp`.

## Tooling

Run commands from this directory.

| Task | Command |
| --- | --- |
| Install deps | `npm ci` |
| Build | `npm run build` |
| Serve | `npm run serve` |

## App Rules

- Keep changes narrow and consistent with the existing AngularJS, Grunt, Less, and template patterns.
- Do not port new Svelte architecture, shadcn-svelte components, TanStack Query/Form, or Svelte routing patterns into this app.
- Preserve existing routes, translation keys, assets, and public behavior unless the user explicitly approves a change.
- If the same change could be made in either frontend, stop and confirm scope before touching Angular.

## Local URLs

- Legacy Angular app: `https://angular-ex.dev.localhost:7121`
- API health: `https://api-ex.dev.localhost:7111/api/v2/about`
- API health fallback for command-line tools with local TLS issues: `http://api-ex.dev.localhost:7110/api/v2/about`

Browser automation and smoke tests must target local URLs unless the user explicitly provides an external URL and asks to use it.
