---
description: "Frontend: Svelte specific guidelines"
applyTo: "src/Exceptionless.Web/ClientApp/**/*.svelte"
---

# Svelte Component Guidelines

## Component Structure

- Use Svelte 5 syntax and features consistently
- Prefer `$state` and `$derived` over `$effect` when possible
- Always use `onclick` instead of `on:click`
- Use `import { page } from '$app/state'` instead of `'$app/stores'`
- Use snippets `{#snippet ...}` and `{@render ...}` instead of `<slot>` for content projection.

## Asynchronous Components (Experimental)

**Available in Svelte 5.36+ with experimental.async compiler option**

You can now use `await` directly in three places:
- At the top level of a component `<script>`
- In a `$derived` expression
- In template expressions (markup)

## Form Handling with Superforms

Always supply a unique, stable `id` option for every `superForm` instance (e.g. `id: 'login'`, `id: 'update-user'`, `id: 'invite-user'`). Missing ids lead to duplicate form data warnings when multiple forms (including dialogs) are present. Use short, kebab-case resource-action names and never reuse the same id on the same page.

### Safe Data Cloning Pattern
Always use the `structuredCloneState()` utility when initializing forms and resetting form data to prevent cache mutation and reactive entanglement:

```svelte
import { structuredCloneState } from '$features/shared/utils/state';

// Form initialization - use structuredCloneState utility
const form = superForm(defaults(structuredCloneState(settings) || new NotificationSettings(), classvalidatorClient(NotificationSettings)), {
    // form options...
});

// Form reset in $effect - use structuredCloneState utility
$effect(() => {
    if (!$submitting && !$tainted && settings !== previousSettingsRef) {
        const clonedSettings = structuredCloneState(settings);
        form.reset({ data: clonedSettings, keepMessage: true });
        previousSettingsRef = settings;
    }
});
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

### Superforms onUpdate Pattern for Dialogs

When using `superForm` inside dialogs, always use the following `onUpdate` pattern to ensure server-side validation errors are applied and dialogs don't close prematurely. This also prevents SvelteKit from stealing focus on success.

```svelte
const form = superForm(defaults(new MyForm(), classvalidatorClient(MyForm)), {
    dataType: 'json',
    id: 'my-form-id',
    async onUpdate({ form, result }) {
        if (!form.valid) {
            return;
        }

        try {
            await doAction(form.data);

            open = false;

            // HACK: Prevent SvelteKit from stealing focus
            result.type = 'failure';
        } catch (error: unknown) {
            if (error instanceof ProblemDetails) {
                applyServerSideErrors(form, error);
                result.status = error.status ?? 500;
            } else {
                result.status = 500;
            }
        }
    },
    SPA: true,
    validators: classvalidatorClient(MyForm)
});
```

Requirements:
- Import and use `ProblemDetails` from `@exceptionless/fetchclient` and `applyServerSideErrors` from `$features/shared/validation`.
- Close the dialog with `open = false` only after the action succeeds.
- Set `result.type = 'failure'` after success to avoid focus theft.

### Dialog Action Functions Must Rethrow

When passing action functions into dialogs (e.g., `save`, `suspend`, `setBonus`), if you display a toast in a `catch` block, you must rethrow the error so the dialog’s `onUpdate` handler can catch it and apply server-side validation errors.

Example:

```ts
async function setBonus(params: PostSetBonusOrganizationParams) {
    toast.dismiss(toastId);
    try {
        await setOrganizationBonus.mutateAsync(params);
        toast.success('Successfully set the organization bonus.');
    } catch (error) {
        const message = error instanceof ProblemDetails ? error.title : 'Please try again.';
        toast.error(`An error occurred while trying to set the organization bonus: ${message}`);
        throw error; // critical: propagate to form
    }
}
```

### Why These Patterns?
- **Prevents Cache Mutation**: `structuredCloneState()` creates independent copies that don't affect cached data
- **Reactive Safety**: Uses `$state.snapshot()` internally for non-reactive snapshots, preventing unintended dependencies
- **Form Isolation**: Each form gets its own copy of data, preventing cross-contamination
- **Auto-Reset**: Local state automatically resets when source data changes
- **Bindable**: Creates writable state for form controls and UI components
- **Predictable Behavior**: Ensures consistent form state management across all scenarios
- **Type Safety**: Utility provides proper TypeScript types and handles undefined/null gracefully

### Reference Comparison for Resets
Use object reference comparison instead of JSON stringification for performance:
```svelte
// ✅ Good - Reference comparison
if (settings !== previousSettingsRef) {
    // reset logic
}

// ❌ Avoid - JSON comparison (slower)
if (JSON.stringify(settings) !== JSON.stringify(previousSettings)) {
    // reset logic
}
```

## Event Handling

- All single-line control statements must be enclosed in curly braces
- Use proper event handling patterns with Svelte 5 syntax

## Component Organization

- Follow kebab-case naming for component files
- Use the Composite Component Pattern
- Organize components within vertical slices aligned with API controllers

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

<Button onclick={() => (openMyActionDialog = true)}>
    Action Label
</Button>

{#if openMyActionDialog}
    <MyActionDialog bind:open={openMyActionDialog} action={performAction} />
{/if}
```

## Accessibility

- Ensure excellent keyboard navigation for all interactions
- Use semantic HTML elements
- Maintain WCAG 2.2 Level AA compliance
- Implement mobile-first design principles

## Reference Documentation

- Always use Svelte 5 features: [https://svelte.dev/llms-full.txt](https://svelte.dev/llms-full.txt)
  - on:click -> onclick
  - import { page } from '$app/stores'; -> import { page } from '$app/state'
  - <slot> -> {#snippet ...}
  - beforeUpdate/afterUpdate -> $effect.pre
