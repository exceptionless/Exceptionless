---
description: "Frontend: Svelte 5 TypeScript SPA guidelines"
applyTo: "src/Exceptionless.Web/ClientApp/**/*.{ts,js,svelte}"
---

# Frontend Guidelines (Svelte 5 / TypeScript SPA)

Located in the `src/Exceptionless.Web/ClientApp` directory.

## Framework & Best Practices

- Use Svelte 5 in SPA mode with TypeScript and Tailwind CSS.
- Code can be formatted and linted with `npm run format` and checked for errors with `npm run check` tasks.
- Limit use of $effect as there is usually a better way to solve the problem like using $derived.
- **Do NOT** use any server-side Svelte features.

## Architecture & Components

- Follow the Composite Component Pattern.
- Organize code into vertical slices (e.g., features aligned with API controllers) and maintain shared components in a central folder.
- Reexport generated code `src/Exceptionless.Web/ClientApp/src/lib/generated` from the respective feature models folder.
  - Always look for models in generated code before creating new models.
- **CRITICAL**: Always examine existing similar components and follow their exact patterns before creating new components.
  - Study naming conventions, state management, and file organization from existing examples.
  - For dialogs, examine `/components/dialogs/` folders for established patterns.
  - For options/dropdowns, check for existing `options.ts` files following the `DropdownItem<EnumType>[]` pattern.

## UI & Accessibility

- Ensure excellent keyboard navigation for all interactions.
- Build forms with shadcn-svelte forms & superforms, and validate with class-validator.
  - Good examples are the manage account and login pages.
- Use formatters `src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/formatters` for displaying built-in types (date, number, boolean, etc.).
- All dialogs should use shadcn-svelte dialog components.
  - Good examples are the mark fixed and delete stack dialogs.
- Ensure semantic HTML, mobile-first design, and WCAG 2.2 Level AA compliance.
- Use shadcn-svelte components (based on [bits-ui](https://bits-ui.com/docs/llms.txt)).
  - Look for new components in the shadcn-svelte documentation
- **Navigation**: Prefer `href` over `onclick` with `goto()` for better linkability, SEO, and accessibility.
  - Users can right-click to open in new tabs, copy links, and screen readers work better
  - Use `onclick` with `goto()` only for complex navigation logic or when additional processing is needed

## API Calls

- Use TanStack Query for all API calls centralized in an `api.svelte.ts` file.
- Leverage `@exceptionless/fetchclient` for network operations.
- **API function naming**: Prefix function names with HTTP verbs (e.g., `postOrganization`, `patchOrganization`, `deleteOrganization`)
- **API interfaces**: Follow the pattern `[HttpVerb][Resource][Params|Request]` (e.g., `PostOrganizationParams`, `DeleteOrganizationRequest`)

## Type Safety & Interfaces

- **Always use existing API interfaces** instead of creating inline types (e.g., use `SuspendOrganizationParams` from `api.svelte.ts`)
- Import type definitions from their proper modules (`api.svelte.ts`, `models.ts`, `options.ts`)
- Create `options.ts` files for dropdown/select data following the `DropdownItem<EnumType>[]` pattern
- Check existing type definitions before creating new ones to avoid duplication
