---
name: Svelte Components
description: |
  Svelte 5 component patterns for the Exceptionless SPA. Runes, reactivity, props,
  events, snippets, component organization, and shadcn-svelte integration.
  Keywords: Svelte 5, $state, $derived, $effect, $props, runes, onclick, snippets,
  {@render}, reactive, component composition, shadcn-svelte
---

# Svelte Components

> **Documentation:** [svelte.dev](https://svelte.dev/docs) | Use `context7` for API reference

## Visual Validation with Chrome MCP

**Always verify UI changes visually** using the Chrome MCP:

1. After making component changes, use Chrome MCP to take a snapshot or screenshot
2. Verify the component renders correctly and matches expected design
3. Test interactive states (hover, focus, disabled) when applicable
4. Check responsive behavior at different viewport sizes
5. Default to the `/next` site path for verification

This visual validation loop catches styling issues, layout problems, and accessibility regressions that automated tests may miss.

## File Organization

### Naming Conventions

- **kebab-case** for all component files: `stack-status-badge.svelte`, `user-profile-card.svelte`
- Co-locate with feature slice, aligned with API controllers

### Directory Structure

```text
src/lib/features/
├── organizations/           # Matches OrganizationController
│   ├── components/
│   │   ├── organization-card.svelte
│   │   └── organization-switcher.svelte
│   ├── api.svelte.ts
│   ├── models.ts
│   └── schemas.ts
├── stacks/                  # Matches StackController
│   └── components/
│       └── stack-status-badge.svelte
└── shared/                  # Shared across features
    └── components/
        ├── data-table/
        ├── navigation/
        └── typography/
```

## Always Use shadcn-svelte Components

**Never use native HTML** for buttons, inputs, or form elements:

```svelte
<script lang="ts">
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import * as Card from '$comp/ui/card';
</script>

<!-- ✅ Use shadcn components -->
<Card.Root>
    <Card.Header>
        <Card.Title>Settings</Card.Title>
    </Card.Header>
    <Card.Content>
        <Input placeholder="Enter value" />
    </Card.Content>
    <Card.Footer>
        <Button>Save</Button>
    </Card.Footer>
</Card.Root>

<!-- ❌ Never use native HTML -->
<button class="...">Save</button>
<input type="text" />
```

## Runes

### $state - Reactive State

```svelte
<script lang="ts">
    let count = $state(0);
    let user = $state<User | null>(null);
    let items = $state<string[]>([]);
</script>
```

### $derived - Computed Values

```svelte
<script lang="ts">
    let count = $state(0);
    let doubled = $derived(count * 2);
    let isEven = $derived(count % 2 === 0);

    // Complex derived
    let summary = $derived.by(() => {
        return items.filter(i => i.active).map(i => i.name).join(', ');
    });
</script>
```

### $effect - Side Effects

```svelte
<script lang="ts">
    let searchTerm = $state('');

    $effect(() => {
        console.log('Search term changed:', searchTerm);
        return () => console.log('Cleaning up');
    });
</script>
```

## Props

```svelte
<script lang="ts">
    interface Props {
        name: string;
        count?: number;
        onUpdate?: (value: number) => void;
        children?: import('svelte').Snippet;
    }

    let { name, count = 0, onUpdate, children }: Props = $props();
</script>
```

## Event Handling

Use `onclick` instead of `on:click`:

```svelte
<Button onclick={() => handleClick()}>Click me</Button>
<Input oninput={(e) => (value = e.currentTarget.value)} />
```

## Snippets (Content Projection)

Replace `<slot>` with snippets. From [login/+page.svelte](src/Exceptionless.Web/ClientApp/src/routes/(auth)/login/+page.svelte):

```svelte
<form.Subscribe selector={(state) => state.errors}>
    {#snippet children(errors)}
        <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
    {/snippet}
</form.Subscribe>

<form.Field name="email">
    {#snippet children(field)}
        <Field.Field data-invalid={ariaInvalid(field)}>
            <Field.Label for={field.name}>Email</Field.Label>
            <Input
                id={field.name}
                value={field.state.value}
                oninput={(e) => field.handleChange(e.currentTarget.value)}
            />
            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
        </Field.Field>
    {/snippet}
</form.Field>
```

## Class Merging

Use array syntax for conditional classes:

```svelte
<div class={['flex items-center', expanded && 'bg-muted', className]}>
    Content
</div>

<Button class={['w-full', isActive && 'bg-primary']}>Save</Button>
```

## Keyboard Accessibility

All interactive components must be keyboard accessible:

- Use `Button` component (provides focus handling automatically)
- Ensure custom interactions have `tabindex` and keyboard handlers
- Test with keyboard-only navigation

See [accessibility](accessibility/SKILL.md) for WCAG guidelines.

## Imports

```svelte
<script lang="ts">
    // Use $app/state instead of $app/stores
    import { page } from '$app/state';

    // Access page data
    let currentPath = $derived(page.url.pathname);
</script>
```

## References

- [shadcn-svelte](shadcn-svelte/SKILL.md) — UI component patterns and trigger snippets
- [accessibility](accessibility/SKILL.md) — WCAG guidelines and keyboard navigation
