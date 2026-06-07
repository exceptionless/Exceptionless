# Svelte ClientApp

This directory is the Svelte 5 frontend. For frontend/UI/page/component/form/route work, use this app unless the user explicitly asks for Angular or legacy UI changes.

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

Use focused verification while iterating. Do not run broad `npm run check`, `npm run lint`, or `npm run format` after every small edit. Run them only for pre-push/pre-PR verification when there are pending unpushed frontend changes in this app, or when the user explicitly asks for them.

## App Rules

- Use Svelte 5 patterns: runes, snippets, Svelte event attributes such as `onclick`, and typed TypeScript.
- Organize code by feature under `src/lib/features`; match the nearest existing feature before adding files.
- Use generated API types and feature-local `api.svelte.ts`, `models.ts`, `schemas.ts`, and `validators.ts` patterns.
- Use TanStack Query for server state and TanStack Form with Zod for forms.
- Use `kit-query-params` for route query parameters instead of ad-hoc URL parsing.
- Prefer shared components and formatters from `$comp`, `$shared`, and `$lib` before creating new primitives.
- Use installed shadcn-svelte components from `$comp/ui/*`; check `components.json` and the `src/lib/features/shared/components/ui` directory before importing a component.
- Do not copy structure, state patterns, or styling patterns from `../ClientApp.angular`.

## Local URLs

- Svelte app: `https://web-ex.dev.localhost:7131/next/`
- API health: `https://api-ex.dev.localhost:7111/api/v2/about`
- API health fallback for command-line tools with local TLS issues: `http://api-ex.dev.localhost:7110/api/v2/about`

Browser automation, E2E tests, and smoke tests must target local URLs unless the user explicitly provides an external URL and asks to use it.
