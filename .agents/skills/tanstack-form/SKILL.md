---
name: TanStack Form
description: |
  TanStack Form with Zod validation in Svelte 5. Form state management, field validation,
  error handling, and ProblemDetails integration.
  Keywords: TanStack Form, createForm, Field, form validation, zod schema, form errors,
  onSubmit, onSubmitAsync, problemDetailsToFormErrors
---

# TanStack Form

> **Documentation:** [tanstack.com/form](https://tanstack.com/form) | Use `context7` for API reference

Use TanStack Form (`@tanstack/svelte-form`) with Zod for form state management.

## Zod Schema Generation

Schemas are generated from backend models and extended in feature slices:

```typescript
// Generated in $generated/schemas.ts (auto-generated from backend)
export const LoginSchema = object({ email: email(), password: string() });
export type LoginFormData = Infer<typeof LoginSchema>;

// Extended in feature schemas.ts
// From src/lib/features/auth/schemas.ts
import { ChangePasswordModelSchema } from '$generated/schemas';

export const ChangePasswordSchema = ChangePasswordModelSchema.extend({
    confirm_password: string().min(6).max(100)
}).refine((data) => data.password === data.confirm_password, {
    message: 'Passwords do not match',
    path: ['confirm_password']
});
export type ChangePasswordFormData = Infer<typeof ChangePasswordSchema>;

// Re-export generated schemas
export { LoginSchema, type LoginFormData } from '$generated/schemas';
```

## Basic Form Pattern

From [login/+page.svelte](src/Exceptionless.Web/ClientApp/src/routes/(auth)/login/+page.svelte):

```svelte
<script lang="ts">
    import { createForm } from '@tanstack/svelte-form';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Button } from '$comp/ui/button';
    import { type LoginFormData, LoginSchema } from '$features/auth/schemas';
    import { ariaInvalid, mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';

    const form = createForm(() => ({
        defaultValues: {
            email: '',
            password: ''
        } as LoginFormData,
        validators: {
            onSubmit: LoginSchema,
            onSubmitAsync: async ({ value }) => {
                const response = await login(value.email, value.password);
                if (response.ok) {
                    await goto('/');
                    return null;
                }
                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));
</script>

<form onsubmit={(e) => { e.preventDefault(); form.handleSubmit(); }}>
    <form.Field name="email">
        {#snippet children(field)}
            <Field.Field data-invalid={ariaInvalid(field)}>
                <Field.Label for={field.name}>Email</Field.Label>
                <Input
                    id={field.name}
                    type="email"
                    value={field.state.value}
                    onblur={field.handleBlur}
                    oninput={(e) => field.handleChange(e.currentTarget.value)}
                    aria-invalid={ariaInvalid(field)}
                />
                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
            </Field.Field>
        {/snippet}
    </form.Field>

    <form.Subscribe selector={(state) => state.isSubmitting}>
        {#snippet children(isSubmitting)}
            <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Logging in...' : 'Log In'}
            </Button>
        {/snippet}
    </form.Subscribe>
</form>
```

## Server Error Handling

Convert ProblemDetails to form errors:

```typescript
onSubmitAsync: async ({ value }) => {
    const response = await login(value.email, value.password);
    if (response.ok) return null;
    return problemDetailsToFormErrors(response.problem);
}
```

## Form in Dialog Pattern

Close dialog only after successful submission:

```svelte
let open = $state(false);

const form = createForm(() => ({
    defaultValues: { name: '' },
    validators: {
        onSubmit: mySchema,
        onSubmitAsync: async ({ value }) => {
            try {
                await createMutation.mutateAsync(value);
                open = false;
                return null;
            } catch (error: unknown) {
                if (error instanceof ProblemDetails) {
                    return problemDetailsToFormErrors(error);
                }
                return { form: 'An unexpected error occurred' };
            }
        }
    }
}));
```

## References

See [shadcn-svelte](shadcn-svelte/SKILL.md) for Field component patterns.
