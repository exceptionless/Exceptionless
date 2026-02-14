---
name: Accessibility
description: |
  WCAG 2.2 AA accessibility standards for the Exceptionless frontend. Semantic HTML, keyboard
  navigation, ARIA patterns, focus management, and form accessibility.
  Keywords: WCAG, accessibility, a11y, ARIA, semantic HTML, keyboard navigation, focus management,
  screen reader, alt text, aria-label, aria-describedby, skip links, focus trap
---

# Accessibility (WCAG 2.2 AA)

## Core Principles

- Semantic HTML elements and ARIA landmarks
- Keyboard-first navigation with visible focus states
- Skip links for main content in layouts
- Inclusive, people-first language

## Semantic HTML

```svelte
<!-- Use semantic elements -->
<header>
    <nav aria-label="Main navigation">
        <a href="/dashboard">Dashboard</a>
        <a href="/projects">Projects</a>
    </nav>
</header>

<main id="main-content">
    <h1>Page Title</h1>
    <section aria-labelledby="section-heading">
        <h2 id="section-heading">Section Title</h2>
        <article>...</article>
    </section>
</main>

<footer>...</footer>
```

## Skip Links

```svelte
<!-- At top of layout -->
<a href="#main-content" class="sr-only focus:not-sr-only focus:absolute ...">
    Skip to main content
</a>
```

## Form Accessibility

### Label Every Control

```svelte
<!-- Visible label -->
<label for="email">Email address</label>
<input id="email" type="email" />

<!-- Or using aria-label for icon-only inputs -->
<input type="search" aria-label="Search events" />
```

### Required Fields

```svelte
<label for="name">Name <span aria-hidden="true">*</span></label>
<input id="name" required aria-required="true" />
```

### Error Messages

```svelte
<input
    id="email"
    aria-invalid={hasError}
    aria-describedby={hasError ? 'email-error' : undefined}
/>
{#if hasError}
    <p id="email-error" class="text-destructive">
        Please enter a valid email address
    </p>
{/if}
```

### Validation Behavior

- On validation failure: focus first invalid input
- Never disable submit just to block validation
- Show inline errors linked via `aria-describedby`

## Keyboard Navigation

### Focus Order

```svelte
<!-- Natural tab order follows DOM order -->
<button>First</button>
<button>Second</button>
<button>Third</button>

<!-- Remove from tab order when hidden -->
<div hidden>
    <button tabindex="-1">Hidden button</button>
</div>
```

### Focus Management in Dialogs

```typescript
// When dialog opens, focus first interactive element
$effect(() => {
    if (open) {
        dialogRef?.querySelector('input, button')?.focus();
    }
});

// When dialog closes, return focus to trigger
const triggerRef = document.activeElement;
// ... on close
triggerRef?.focus();
```

### Keyboard Shortcuts

```svelte
<button
    onkeydown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            handleClick();
        }
    }}
>
    Action
</button>
```

## Images and Icons

### Informative Images

```svelte
<img src={user.avatar} alt={`Profile photo of ${user.name}`} />
```

### Decorative Images

```svelte
<img src="/decorative-pattern.svg" alt="" aria-hidden="true" />
```

### Icon Buttons

```svelte
<button aria-label="Delete event">
    <TrashIcon aria-hidden="true" />
</button>
```

### Icons with Text

```svelte
<button>
    <PlusIcon aria-hidden="true" />
    Add Event
</button>
```

## ARIA Patterns

### Live Regions

```svelte
<!-- For dynamic updates (notifications, loading states) -->
<div aria-live="polite" aria-atomic="true">
    {#if loading}
        Loading events...
    {/if}
</div>

<!-- For urgent messages -->
<div role="alert">
    Error: Failed to save changes
</div>
```

### Expandable Content

```svelte
<button
    aria-expanded={isExpanded}
    aria-controls="panel-content"
>
    {isExpanded ? 'Collapse' : 'Expand'}
</button>
<div id="panel-content" hidden={!isExpanded}>
    Panel content
</div>
```

### Tabs

```svelte
<div role="tablist" aria-label="Event tabs">
    <button role="tab" aria-selected={activeTab === 'details'}>
        Details
    </button>
    <button role="tab" aria-selected={activeTab === 'stack'}>
        Stack Trace
    </button>
</div>
<div role="tabpanel" aria-labelledby="details-tab">
    Tab content
</div>
```

## Color and Contrast

- Minimum contrast ratio: 4.5:1 for normal text
- 3:1 for large text and UI components
- Don't rely on color alone to convey information

```svelte
<!-- ✅ Good: Icon + color + text -->
<span class="text-destructive">
    <AlertIcon aria-hidden="true" />
    Error: Invalid input
</span>

<!-- ❌ Bad: Color only -->
<span class="text-destructive">Invalid input</span>
```

## Testing Accessibility

```bash
# Run axe-playwright audits in E2E tests
npm run test:e2e
```

```typescript
// In Playwright tests
import AxeBuilder from '@axe-core/playwright';

test('page is accessible', async ({ page }) => {
    await page.goto('/dashboard');
    const results = await new AxeBuilder({ page }).analyze();
    expect(results.violations).toEqual([]);
});
```
