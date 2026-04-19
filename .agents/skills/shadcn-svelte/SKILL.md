---
name: shadcn-svelte
description: >
    Use this skill when building UI with shadcn-svelte or bits-ui components — buttons, dialogs,
    sheets, popovers, dropdowns, tooltips, forms, inputs, or selects. Covers import patterns,
    trigger snippets, child snippet composition, and the cn utility. Apply when adding or
    customizing any shadcn-svelte component in the frontend.
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

## Trigger Components — Child Snippet Pattern

When using trigger components with custom elements like Button, **always use the `child` snippet pattern**:

```svelte
<!-- ✅ Correct: Single tab stop, proper ARIA delegation -->
<Tooltip.Trigger>
    {#snippet child({ props })}
        <Button {...props} variant="ghost" size="icon">
            <Icon />
        </Button>
    {/snippet}
</Tooltip.Trigger>
```

```svelte
<!-- ❌ Wrong: Creates two focusable elements (double-tab issue) -->
<Tooltip.Trigger>
    <Button>Content</Button>
</Tooltip.Trigger>
```

Apply this pattern to **all** triggers: `DropdownMenu.Trigger`, `Popover.Trigger`, `Dialog.Trigger`, `Tooltip.Trigger`.

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
                <Dialog.Description>Add a new organization to your account.</Dialog.Description>
            </Dialog.Header>
            <!-- Form content -->
            <Dialog.Footer>
                <Button variant="outline" onclick={() => (openCreateDialog = false)}>Cancel</Button>
                <Button type="submit">Create</Button>
            </Dialog.Footer>
        </Dialog.Content>
    </Dialog.Root>
{/if}
```

**Naming**: Use `open[ComponentName]Dialog` (e.g. `openSuspendOrganizationDialog`). Avoid generic `showDialog` or `isOpen`.

## DropdownMenu

```svelte
<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} variant="outline">Select Status</Button>
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

## Class Merging with Array Syntax

Use Svelte array syntax for conditional classes (NOT `cn` utility):

```svelte
<!-- ✅ Preferred: Array syntax -->
<Button class={['w-full', isActive && 'bg-primary']}>Click me</Button>

<!-- ❌ Avoid: cn utility (older pattern) -->
<Button class={cn('w-full', isActive && 'bg-primary')}>
```
