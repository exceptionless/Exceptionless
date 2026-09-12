---
name: tanstack-form
description: Build Svelte forms with TanStack Form, Zod validation, and API error mapping.
---

# TanStack Form

Use `@tanstack/svelte-form` with feature schemas derived from `$generated/schemas`. Extend schemas in feature-owned files; do not edit generated output.

## Current examples

- `src/Exceptionless.Web/ClientApp/src/routes/(auth)/login/+page.svelte`: form creation, submission, field composition, and submitting state.
- `src/Exceptionless.Web/ClientApp/src/lib/features/auth/schemas.ts`: generated schema exports and feature-specific validation.
- `src/Exceptionless.Web/ClientApp/src/lib/features/shared/validation.ts`: `ariaInvalid`, `mapFieldErrors`, and `problemDetailsToFormErrors`.

Follow the current implementations rather than duplicating form infrastructure. Map API ProblemDetails into field/form errors, retain entered values after failure, and close dialogs only after successful submission.

Use installed `Field` components with associated labels and validation attributes. Disable duplicate submission while pending. See [shadcn-svelte](../shadcn-svelte/SKILL.md) for component composition.

## Submission lifecycle

1. Define typed default values and extend generated Zod schemas for feature-specific or cross-field constraints.
2. Use the current form's synchronous validation hook for schema errors and asynchronous submission hook for the server operation. Follow the referenced implementation's hook contracts rather than moving API calls between hooks blindly.
3. Await the operation. Map expected ProblemDetails with `problemDetailsToFormErrors`; provide a form-level error for an unexpected failure without exposing internal details.
4. Connect field state through `ariaInvalid` and `mapFieldErrors`. Display form-level errors as well as field errors so non-field failures remain visible.
5. Keep pending state active through required work. Preserve values on failure; reset, navigate, or close a dialog only after success and any required cache reconciliation.

Check invalid input, server rejection, repeated submission, and successful completion. A dialog closing is not by itself proof that the save succeeded.
