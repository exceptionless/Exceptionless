---
name: frontend-architecture
description: Apply Exceptionless Svelte conventions when adding or restructuring frontend features.
---

# Frontend Architecture

Work in `src/Exceptionless.Web/ClientApp` and follow its `AGENTS.md`.

## Feature structure

Keep feature behavior in `src/lib/features/<feature>/`: API operations in `api.svelte.ts`, local types in `models.ts`, validation in `schemas.ts` or `validators.ts`, and UI in `components/`. Reuse generated API types through feature aliases. Match the nearest current feature instead of copying a static scaffold.

- [tanstack-query](../tanstack-query/SKILL.md): server state, mutation behavior, and WebSocket invalidation.
- [tanstack-form](../tanstack-form/SKILL.md): schema validation and API error mapping.
- [shadcn-svelte](../shadcn-svelte/SKILL.md): installed component composition.

## Local UI conventions

- Reuse `$comp`, `$shared`, and `$lib` helpers. In particular, use `createQueryParameters` for route query binding.
- Use shared formatter components in `src/lib/features/shared/components/formatters/` for dates, relative time, duration, bytes, numbers, currency, percentages, booleans, and date math.
- Match the existing dense, restrained operational UI and its theme tokens.
- Use `href` for ordinary navigation; use action handlers when work must finish before navigating.
- Prefer derived state for computed values; reserve effects for side effects. Use current Svelte event attributes and snippets.
- Preserve accessible labels, focus behavior, dialog titles, and field-error associations.

## Svelte and TypeScript details

- Use `options.ts` for shared option sets within a feature. Use `$features` for feature imports and `$generated` for generated contracts; extend generated types in feature-owned files.
- Prefer named imports; use namespace imports for composite components such as `Dialog`, `DropdownMenu`, `Field`, and `Card`.
- Keep files and directories kebab-case. Follow the app's ESLint rules for braces and statement formatting.
- Avoid `any`; use generated types, explicit interfaces, `unknown`, and type guards. Spell out identifiers such as `organization` and `filter`.
- Await asynchronous work and handle failures at the appropriate local boundary. Do not let rejected promises bypass error UI or mark an operation complete before its required work finishes.
- Use `$derived` for computed values and `$effect` for side effects. Use `untrack()` selectively when an incidental read should not become an effect dependency; do not hide dependencies needed for correctness.
- Use Svelte event attributes (`onclick`, `oninput`) and snippets. Use array class syntax or `cn()` for conditional classes.

## Accessibility and interaction

- Preserve native semantic elements, landmarks, and a clear heading hierarchy. Shared UI components complement document semantics rather than replacing them with styled `div`s.
- Associate each control with a label. Give icon-only buttons an accessible name and hide decorative icons from assistive technology.
- Give dialogs a title, even when visually hidden. Preserve keyboard navigation and visible focus states.
- Set `aria-invalid` on invalid controls and connect help/error messages with `aria-describedby`.
- For motion work, consult the frontend's installed `emil-design-eng` skill. Apply its interaction principles through existing Svelte components, preserving reduced-motion behavior and the operational UI's restraint.

## Verification

Use colocated Vitest/Testing Library tests and local Playwright flows as appropriate. Prefer role and label queries. Follow the app's validation command policy; inspect the rendered flow when UI behavior changes.
