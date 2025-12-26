# Svelte Component Guidelines

Applies to Svelte components under `src/Exceptionless.Web/ClientApp/src`. Follow the ClientApp AGENT for general frontend, testing, and accessibility expectations.

## Component Structure

- Use Svelte 5 syntax and features consistently
- Prefer `$state` and `$derived` over `$effect` when possible
- Always use `onclick` instead of `on:click`
- Use `import { page } from '$app/state'` instead of `'$app/stores'`
- Use snippets `{#snippet ...}` and `{@render ...}` instead of `<slot>` for content projection.

## Asynchronous Components (Experimental)

Available in Svelte 5.36+ with experimental.async compiler option

You can now use `await` directly in three places:

- At the top level of a component `<script>`
- In a `$derived` expression
- In template expressions (markup)

## Form Handling with TanStack Form + Zod

Use TanStack Form (`@tanstack/svelte-form`) with Zod for form state management and validation.

### Key Concepts

- **Zod via Standard Schema**: TanStack Form works directly with Zod 4+ via [Standard Schema](https://github.com/standard-schema/standard-schema) - no adapter needed!
- **Schemas location**: Create Zod schemas in `schemas.ts` files next to models in each feature slice
- **Field components**: Use shadcn-svelte Field components (`$comp/ui/field`) for form field layout

### Basic Form Pattern

```svelte
<script lang="ts">
    import { createForm } from '@tanstack/svelte-form';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { mySchema, type MyFormData } from '$features/myfeature/schemas';
    import { mapFieldErrors, problemDetailsToFormErrors } from '$shared/validation';

    const form = createForm(() => ({
        defaultValues: {
            email: '',
            name: ''
        } as MyFormData,
        validators: {
            onSubmit: mySchema, // Zod schema for client-side validation
            onSubmitAsync: async ({ value }) => {
                const response = await apiCall(value);
                if (response.ok) {
                    await goto('/success');
                    return null;
                }
                // Convert server errors to TanStack Form format
                return problemDetailsToFormErrors(response.problem);
            }
        }
    }));
</script>

<form
    onsubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        form.handleSubmit();
    }}
>
    <!-- Form-level error display -->
    <form.Subscribe selector={(state) => state.errors}>
        {#snippet children(errors)}
            {@const formError = errors.length > 0 ? (typeof errors[0] === 'string' ? errors[0] : (errors[0] as { form?: string })?.form) : undefined}
            <ErrorMessage message={formError} />
        {/snippet}
    </form.Subscribe>

    <!-- Field with validation -->
    <form.Field name="email">
        {#snippet children(field)}
            <Field.Field data-invalid={ariaInvalid(field)}>
                <Field.Label for={field.name}>Email</Field.Label>
                <Input
                    id={field.name}
                    name={field.name}
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

    <!-- Submit button with loading state -->
    <form.Subscribe selector={(state) => state.isSubmitting}>
        {#snippet children(isSubmitting)}
            <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Saving...' : 'Save'}
            </Button>
        {/snippet}
    </form.Subscribe>
</form>
```

### Creating Zod Schemas

Place schemas in `schemas.ts` next to models in each feature slice:

```typescript
// src/lib/features/auth/schemas.ts
import { z } from 'zod';

export const loginSchema = z.object({
    email: z.string().min(1, 'Email is required').email('Invalid email address'),
    password: z.string().min(6, 'Password must be at least 6 characters').max(100),
    invite_token: z.string().length(40).optional()
});

export type LoginFormData = z.infer<typeof loginSchema>;
```

### Server-Side Error Handling

Use `problemDetailsToFormErrors()` to convert API errors. There are two patterns depending on the API style:

**Pattern 1: APIs that throw exceptions (TanStack Query mutations)**

```typescript
import { ProblemDetails } from '@exceptionless/fetchclient';
import { problemDetailsToFormErrors } from '$shared/validation';

onSubmitAsync: async ({ value }) => {
    try {
        await createMutation.mutateAsync(value);
        return null; // Success
    } catch (error: unknown) {
        if (error instanceof ProblemDetails) {
            return problemDetailsToFormErrors(error);
        }
        return { form: 'An unexpected error occurred.' };
    }
};
```

**Pattern 2: APIs that return response objects (auth calls)**

```typescript
import { problemDetailsToFormErrors } from '$shared/validation';

onSubmitAsync: async ({ value }) => {
    const response = await login(value.email, value.password);
    if (response.ok) {
        return null; // Success
    }
    return problemDetailsToFormErrors(response.problem);
};
```

The `problemDetailsToFormErrors` function:

- Extracts form-level errors from `problem.errors.general` or `problem.title`
- Extracts field-level errors for 422 validation responses
- Returns `null` if no errors

### Form in Dialogs Pattern

For forms inside dialogs, close the dialog only after successful submission:

```svelte
<script lang="ts">
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { problemDetailsToFormErrors } from '$shared/validation';

    let open = $state(false);

    const form = createForm(() => ({
        defaultValues: { name: '' },
        validators: {
            onSubmit: mySchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    await createMutation.mutateAsync(value);
                    open = false; // Close dialog on success
                    return null;
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        return problemDetailsToFormErrors(error);
                    }
                    return { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));
</script>

{#if open}
    <Dialog.Root bind:open>
        <Dialog.Content>
            <form
                onsubmit={(e) => {
                    e.preventDefault();
                    form.handleSubmit();
                }}
            >
                <!-- form fields -->
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}
```

### Reactive Binding Pattern

For simple reactive bindings to query data, you can override derived values for binding:

```svelte
// Derived value that can be temporarily overridden for binding
let emailNotificationsEnabled = $derived(meQuery.data?.email_notifications_enabled ?? false);

// Sync the derived value when source data changes
$effect(() => {
    emailNotificationsEnabled = meQuery.data?.email_notifications_enabled ?? false;
});
```

```svelte
<!-- Direct binding works - temporarily overrides derived value -->
<Switch bind:checked={emailNotificationsEnabled} />
```

**Note:** This pattern uses Svelte 5's ability to override derived values (available since v5.25). The derived value automatically recalculates when dependencies change, but can be temporarily overridden for UI binding. The `$effect` ensures the local state resyncs when the source data changes.

### Form Initialization Patterns

**Simple forms** can initialize directly with inline objects or query data:

```svelte
// Inline default values
const form = createForm(() => ({
    defaultValues: {
        name: '',
        email: ''
    } as MyFormData,
    // ...
}));

// Query data with fallback
const form = createForm(() => ({
    defaultValues: {
        email_address: meQuery.data?.email_address ?? ''
    } as UpdateEmailFormData,
    // ...
}));
```

**Reactive forms** that reset when props change need `structuredCloneState()` to prevent cache mutation:

```svelte
import { structuredCloneState } from '$features/shared/utils/state';

// Clone initial data to prevent cache mutation
const initialData = structuredCloneState(range) ?? { end: '', start: '' };

const form = createForm(() => ({
    defaultValues: initialData,
    // ...
}));

// Reset form when prop changes
$effect(() => {
    if (range !== previousRange) {
        const clonedRange = structuredCloneState(range) ?? { end: '', start: '' };
        form.reset();
        form.setFieldValue('start', clonedRange.start);
        // ...
    }
});
```

**When to use `structuredCloneState()`:**

- Forms that reset reactively based on prop changes
- Initializing from TanStack Query data that will be mutated by the form

**When NOT to use:**

- Static forms with hardcoded defaults
- Forms that don't reset after initialization

### Why These Patterns?

- **No Adapter Needed**: Zod 4+ works via Standard Schema specification directly
- **Validation on Submit**: Using `onSubmit` prevents showing errors on untouched fields
- **Server Error Integration**: `onSubmitAsync` handles API errors seamlessly
- **Type Safety**: Zod schemas provide full TypeScript inference
- **Prevents Cache Mutation**: `structuredCloneState()` creates independent copies

## Event Handling

- Use proper event handling patterns with Svelte 5 syntax

## Component Organization

- Follow kebab-case naming for component files
- Use the Composite Component Pattern
- Organize components within vertical slices aligned with API controllers

## shadcn-svelte Trigger Components

When using shadcn-svelte trigger components (Tooltip.Trigger, Popover.Trigger, DropdownMenu.Trigger, etc.) with custom components like Button, **always use the `child` snippet pattern** to avoid double-tab issues and ensure proper accessibility.

### Correct Pattern

```svelte
<Tooltip.Root>
    <Tooltip.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="ghost" size="icon">
                <Icon />
            </Button>
        {/snippet}
    </Tooltip.Trigger>
    <Tooltip.Content>Tooltip text</Tooltip.Content>
</Tooltip.Root>
```

### Why This Pattern?

- **Single Tab Stop**: Creates only one focusable element in the tab order
- **Proper Props Delegation**: The `props` from the trigger are properly spread to the Button
- **Accessibility**: Maintains ARIA attributes and keyboard navigation
- **Official Pattern**: This is the documented shadcn-svelte/bits-ui pattern

### Wrong Patterns

```svelte
<!-- ❌ Wrong: Creates two focusable elements (double-tab issue) -->
<Tooltip.Trigger>
    <Button>Content</Button>
</Tooltip.Trigger>

<!-- ❌ Wrong: Manual styling replicates button styles (maintenance burden) -->
<Tooltip.Trigger class="hover:bg-accent inline-flex...">
    <Icon />
</Tooltip.Trigger>
```

### When to Use

Apply this pattern for **all trigger components** when wrapping custom interactive elements:

- `Tooltip.Trigger`
- `Popover.Trigger`
- `DropdownMenu.Trigger`
- `Dialog.Trigger`
- Any other component with a `.Trigger` that wraps interactive elements

### When NOT to Use

You don't need the `child` snippet when:

- Trigger content is simple text or non-interactive elements
- The trigger itself has no nested focusable elements
- You're using the trigger's native button functionality directly

### Additional Examples

**DropdownMenu with Button:**

```svelte
<DropdownMenu.Trigger>
    {#snippet child({ props })}
        <Button {...props} variant="outline">
            Open Menu
            <ChevronDown />
        </Button>
    {/snippet}
</DropdownMenu.Trigger>
```

**Popover with Button:**

```svelte
<Popover.Trigger>
    {#snippet child({ props })}
        <Button {...props} variant="outline" class="w-[280px]">
            Select Date
            <CalendarIcon />
        </Button>
    {/snippet}
</Popover.Trigger>
```

## Dialog Component Patterns

### Naming Conventions

- Dialog state variables should use `open[ComponentName]Dialog` pattern (e.g., `openSuspendOrganizationDialog`, `openMarkStackDiscardedDialog`)
- Avoid generic names like `showDialog` or `isOpen`

### Event Handlers

- Use inline arrow functions for opening dialogs: `onclick={() => (openDialogName = true)}`
- Avoid creating separate handler functions just to set state to true
- Create separate async functions only for complex operations (API calls, validation, etc.)

### Conditional Rendering

- Always wrap dialogs in `{#if}` blocks: `{#if openDialogName} <Dialog /> {/if}`
- This prevents unnecessary DOM creation and improves performance

### API Integration

- Import and use existing interface types from API files (e.g., `SuspendOrganizationParams`)
- Don't create inline types when proper interfaces exist
- Create options files following the `DropdownItem<EnumType>[]` pattern in `options.ts`

### Example Pattern

```svelte
<script lang="ts">
    import type { ApiParamsInterface } from '$features/module/api.svelte';
    import { optionsArray } from '$features/module/options';

    let openMyActionDialog = $state(false);

    async function performAction(params: ApiParamsInterface) {
        // API call logic here
    }
</script>

<Button onclick={() => (openMyActionDialog = true)}>Action Label</Button>

{#if openMyActionDialog}
    <MyActionDialog bind:open={openMyActionDialog} action={performAction} />
{/if}
```

## Accessibility

For detailed accessibility patterns (WCAG 2.2 AA), see [ClientApp/AGENTS.md](../AGENTS.md#accessibility-wcag-22-aa).

## Reference Documentation

- Always use Svelte 5 features: [https://svelte.dev/llms-full.txt](https://svelte.dev/llms-full.txt)
    - on:click -> onclick
    - import { page } from '$app/stores'; -> import { page } from '$app/state'
    - <slot> -> {#snippet ...}
    - beforeUpdate/afterUpdate -> $effect.pre
