# Svelte ClientApp

This directory is the only Exceptionless application frontend. Svelte 5 serves the app at `/`. All frontend/UI/page/component/form/route work belongs here.

Use root routes and `$app/paths` resolution for maintained navigation. `/next`, `#!/`, and `#/` are supported only as incoming compatibility links. Keep their coverage in `legacy-links` tests, and preserve `satellizer_token` so existing sessions survive the cutover.

Stack action links from emails require authentication and explicit confirmation. Opening, reloading, or cancelling an action link must never mutate the stack. Keep confirmation and failed-update recovery covered alongside incoming-link compatibility.

## Tooling

Run commands from this directory.

| Task                | Command                   |
| ------------------- | ------------------------- |
| Install deps        | `npm ci`                  |
| Dev server          | `npm run dev`             |
| Build               | `npm run build`           |
| Unit tests          | `npm run test:unit`       |
| E2E tests           | `npm run test:e2e`        |
| Storybook           | `npm run storybook`       |
| Generate API models | `npm run generate-models` |

Use focused verification while iterating. Do not run broad Svelte validation after every small edit. Run `npm run validate` only for pre-push/pre-PR verification when there are pending unpushed frontend changes in this app, or when the user explicitly asks for it. This command formats files, so check `git status` afterward and include any formatting changes in the same commit.

## App Rules

- Before adding tests, check existing coverage and name the distinct regression being protected. Extend or parameterize an existing scenario where possible; do not duplicate coverage or test framework/implementation details just to add tests.
- Prefer Vitest for logic, data matrices, and component state; reserve Playwright for real browser integration, layout, and user journeys. Share expensive setup within a coherent journey using `test.step`, while retaining useful failure diagnostics. Keep representative browser coverage when moving repeated cases to Vitest.
- Browser shards use isolated application instances and one worker each because some tests change the shared administrator's preferences. New tests must own and clean up their data and must not depend on file order. Use condition-based waits and bounded request assertions; retain intentional observation windows only when testing the absence of repeated work.
- Use Svelte 5 patterns: runes, snippets, Svelte event attributes such as `onclick`, and typed TypeScript.
- Organize code by feature under `src/lib/features`; match the nearest existing feature before adding files.
- Use generated API types and feature-local `api.svelte.ts`, `models.ts`, `schemas.ts`, and `validators.ts` patterns.
- Use TanStack Query for server state and TanStack Form with Zod for forms.
- Use `createQueryParameters` from `$shared/query-params` for route query parameters instead of ad-hoc URL parsing.
- Prefer shared components and formatters from `$comp`, `$shared`, and `$lib` before creating new primitives.
- Use installed shadcn-svelte components from `$comp/ui/*`; check `components.json` and the `src/lib/features/shared/components/ui` directory before importing a component.

## Applying Installed Skills

- Repository conventions govern application changes when an upstream skill uses generic examples. Keep the existing operational UI, theme, and typography; an ordinary component change does not require a redesign.
- Run component CLI commands from this directory with `npx shadcn-svelte@latest`. Resolve imports through `components.json`; this app uses `$comp/ui/*`. Use `@lucide/svelte` unless the app's configured icon library changes.
- Import only installed components. Use existing `RadioGroup` or `Select` for options and `Card`, `Alert`, or other installed primitives for empty states; add `ToggleGroup`, `Empty`, or other registry components only when the requested design needs them.

## Local URLs

- Svelte app: `https://web-ex.dev.localhost:7131/`
- API health: `https://api-ex.dev.localhost:7111/api/v2/about`
- API health fallback for command-line tools with local TLS issues: `http://api-ex.dev.localhost:7110/api/v2/about`

Browser automation, E2E tests, and smoke tests must target local URLs unless the user explicitly provides an external URL and asks to use it.
