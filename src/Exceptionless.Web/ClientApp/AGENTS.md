# Frontend Guidelines

Located in `src/Exceptionless.Web/ClientApp`. The Svelte SPA is the primary client; avoid using any server-side Svelte features.

## Stack & Commands

- **Framework:** Svelte 5 SPA with TypeScript, Tailwind, TanStack Query, shadcn-svelte/bits-ui
- **Package Manager:** npm with committed lockfile (`npm ci` for reproducible installs)

### Development

```bash
npm ci                   # Install dependencies
npm run dev              # Local API
npm run generate-models  # Regenerate API types
```

### Quality Checks

```bash
npm run format         # Run first when lint fails
npm run lint           # ESLint
npm run check          # Svelte check
```

### Build

```bash
npm run build
```

Use the chrome MCP to verify visual/functional correctness; default to the `/next` site path.

## Architecture & Components

- Follow the Composite Component Pattern; organize code by vertical slices aligned to API controllers. Keep shared pieces in shared folders.
- Re-export generated models from `src/lib/generated` through feature model folders. Look for existing models/options before adding new ones.
- **Critical:** study existing components before creating new ones (naming, state management, file layout). Dialogs: see `/components/dialogs/`. Dropdowns: use `options.ts` with `DropdownItem<EnumType>[]`.
- Use shadcn-svelte components (bits-ui). Prefer `href` navigation over `onclick`/`goto` unless navigation logic requires handling.

## TypeScript / JavaScript

- Follow ESLint + Prettier config; use modern ES6+ practices. All single-line control statements need braces.
- Prefer named imports; avoid namespace imports except approved barrels/shadcn components. Use kebab-case for files/directories.
- Always await Promises; handle errors with try/catch. Avoid `any`—use interfaces/types and guards.

## API & Data

- Centralize API calls in `api.svelte.ts` per feature using TanStack Query. Use `@exceptionless/fetchclient`.
- Name functions with HTTP verb prefixes (`postOrganization`, `patchOrganization`, etc.). Interfaces follow `[HttpVerb][Resource][Params|Request]`.
- Prefer existing generated interfaces/options over inline types.

## UI Patterns

- Build forms with TanStack Form (`@tanstack/svelte-form`) and validate with Zod schemas.
    - Create schemas in `schemas.ts` files next to models in each feature slice.
    - See the manage account/login page for a complete example.
- Use formatters in `src/lib/features/shared/components/formatters` when rendering built in types (boolean, date, number, etc).

## Accessibility (WCAG 2.2 AA)

### Core Principles

- Semantic HTML elements and ARIA landmarks
- Keyboard-first navigation with visible focus states
- Skip links for main content in layouts
- Inclusive, people-first language

### Forms

- Label every control with associated visible labels
- Required fields: `aria-required="true"`
- Inline errors: link via `aria-describedby`
- On validation failure: focus first invalid input
- Never disable submit just to block validation

### Images & Icons

- **Informative:** Meaningful `alt` text or `aria-label`
- **Decorative:** `alt=""` or `aria-hidden="true"`

### Composite Widgets

- Focus management: roving tabindex or `aria-activedescendant`
- Keep static elements out of tab order
- Avoid `menu`/`menubar` roles for site navigation

## Testing

### Unit & Component Tests

```bash
npm run test:unit
```

- **Framework:** Vitest + @testing-library/svelte
- **Location:** Co-locate with code as `.test.ts` or `.spec.ts`
- **Queries:** Use role, label, or text selectors (not implementation details)
- **Mocking:** Use `vi.mock()` for dependencies
- **Pattern:** AAA (Arrange, Act, Assert) with descriptive `describe`/`it` blocks

### E2E Tests

```bash
npx playwright install  # first time only
npm run test:e2e
```

- **Framework:** Playwright with Page Object Model
- **Location:** `e2e/` folder (see `e2e/AGENTS.md` for patterns)
- **Selectors:** Semantic first, `data-testid` fallback
- **Coverage:** Chrome, Firefox, Safari + responsive viewports
- **Accessibility:** Use axe-playwright for automated audits

## API Type Generation

- When API contracts change, run `npm run generate-models` to refresh generated types. Prefer regeneration over hand-writing DTOs.
