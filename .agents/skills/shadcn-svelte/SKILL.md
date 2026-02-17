---
name: shadcn-svelte Components
description: |
  UI components with shadcn-svelte and bits-ui. Component patterns, trigger snippets,
  dialog handling, and accessibility.
  Keywords: shadcn-svelte, bits-ui, Button, Dialog, Sheet, Popover, DropdownMenu,
  Tooltip, Form, Input, Select, child snippet, trigger pattern, cn utility
---

# shadcn-svelte Components

> **Documentation:** [shadcn-svelte.com](https://www.shadcn-svelte.com/) | Use `context7` for API reference

Use shadcn-svelte components (bits-ui) for UI. Import with namespace pattern.

## Import Pattern

```svelte
<script lang="ts">
    import * as Dialog from '$comp/ui/dialog';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Tooltip from '$comp/ui/tooltip';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
</script>
```

## Trigger Components - Child Snippet Pattern

When using trigger components with custom elements like Button, **always use the `child` snippet pattern**:

```svelte
<!-- ✅ Correct: Single tab stop, proper accessibility -->
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

- **Single Tab Stop**: Creates only one focusable element
- **Proper Props Delegation**: ARIA attributes pass through correctly
- **Accessibility**: Maintains keyboard navigation
- **Official Pattern**: Documented shadcn-svelte/bits-ui pattern

### Wrong Patterns

```svelte
<!-- ❌ Wrong: Creates two focusable elements (double-tab issue) -->
<Tooltip.Trigger>
    <Button>Content</Button>
</Tooltip.Trigger>

<!-- ❌ Wrong: Manual styling replicates button styles -->
<Tooltip.Trigger class="hover:bg-accent inline-flex...">
    <Icon />
</Tooltip.Trigger>
```

### Apply to All Triggers

```svelte
<!-- DropdownMenu -->
<DropdownMenu.Trigger>
    {#snippet child({ props })}
        <Button {...props} variant="outline">
            Open Menu
            <ChevronDown />
        </Button>
    {/snippet}
</DropdownMenu.Trigger>

<!-- Popover -->
<Popover.Trigger>
    {#snippet child({ props })}
        <Button {...props} variant="outline" class="w-70">
            Select Date
            <CalendarIcon />
        </Button>
    {/snippet}
</Popover.Trigger>

<!-- Dialog -->
<Dialog.Trigger>
    {#snippet child({ props })}
        <Button {...props}>Open Dialog</Button>
    {/snippet}
</Dialog.Trigger>
```

## Dialog Pattern

```svelte
<script lang="ts">
    import * as Dialog from '$comp/ui/dialog';
    import { Button } from '$comp/ui/button';

    let openCreateDialog = $state(false);
</script>

<Button onclick={() => (openCreateDialog = true)}>Create</Button>

{#if openCreateDialog}
    <Dialog.Root bind:open={openCreateDialog}>
        <Dialog.Content>
            <Dialog.Header>
                <Dialog.Title>Create Organization</Dialog.Title>
                <Dialog.Description>
                    Add a new organization to your account.
                </Dialog.Description>
            </Dialog.Header>

            <!-- Form content -->

            <Dialog.Footer>
                <Button variant="outline" onclick={() => (openCreateDialog = false)}>
                    Cancel
                </Button>
                <Button type="submit">Create</Button>
            </Dialog.Footer>
        </Dialog.Content>
    </Dialog.Root>
{/if}
```

## Dialog Naming Convention

- Use `open[ComponentName]Dialog` pattern
- Avoid generic names like `showDialog` or `isOpen`

```svelte
<script lang="ts">
    let openSuspendOrganizationDialog = $state(false);
    let openMarkStackDiscardedDialog = $state(false);
    let openInviteUserDialog = $state(false);
</script>
```

## DropdownMenu with Options

```svelte
<script lang="ts">
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { statusOptions } from './options';
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="outline">
                Select Status
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content>
        {#each statusOptions as option}
            <DropdownMenu.Item onclick={() => handleSelect(option.value)}>
                {option.label}
            </DropdownMenu.Item>
        {/each}
    </DropdownMenu.Content>
</DropdownMenu.Root>
```

## Options File Pattern

```typescript
// options.ts
import type { DropdownItem } from '$shared/types';

export enum Status {
    Active = 'active',
    Inactive = 'inactive',
    Pending = 'pending'
}

export const statusOptions: DropdownItem<Status>[] = [
    { value: Status.Active, label: 'Active' },
    { value: Status.Inactive, label: 'Inactive' },
    { value: Status.Pending, label: 'Pending' }
];
```

## Sheet (Slide-out Panel)

```svelte
<Sheet.Root bind:open={openFiltersSheet}>
    <Sheet.Content side="right">
        <Sheet.Header>
            <Sheet.Title>Filters</Sheet.Title>
        </Sheet.Header>

        <!-- Filter controls -->

        <Sheet.Footer>
            <Button onclick={applyFilters}>Apply</Button>
        </Sheet.Footer>
    </Sheet.Content>
</Sheet.Root>
```

## Class Merging with Array Syntax

Use Svelte array syntax for conditional classes (NOT cn utility):

```svelte
<!-- ✅ Preferred: Array syntax -->
<Button class={['w-full', isActive && 'bg-primary']}>
    Click me
</Button>

<div class={['flex items-center', expanded && 'bg-muted', className]}>
    Content
</div>

<!-- ❌ Avoid: cn utility (older pattern) -->
<Button class={cn('w-full', isActive && 'bg-primary')}>
```

## Navigation Preference

Prefer `href` navigation over `onclick`/`goto`:

```svelte
<!-- ✅ Preferred: Native navigation -->
<Button href="/organizations/new">Create</Button>

<!-- Use onclick only when navigation logic required -->
<Button onclick={async () => {
    await saveData();
    goto('/success');
}}>
    Save and Continue
</Button>
```
