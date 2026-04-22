---
name: accessibility
description: >
    Use this skill when building or reviewing frontend components for accessibility compliance.
    Covers WCAG 2.2 AA standards including semantic HTML, keyboard navigation, ARIA patterns,
    focus management, screen reader support, and form accessibility. Apply when creating new
    UI components, fixing accessibility bugs, adding skip links or focus traps, or ensuring
    inclusive markup — even if the user doesn't explicitly mention "a11y" or "WCAG."
---

# Accessibility (WCAG 2.2 AA)

## Core Principles

- Semantic HTML elements and ARIA landmarks
- Keyboard-first navigation with visible focus states
- Skip links for main content in layouts
- Inclusive, people-first language

## Semantic HTML

Use `<header>`, `<nav aria-label="...">`, `<main id="main-content">`, `<section aria-labelledby="...">`, `<footer>`. Always use heading hierarchy (`h1` → `h2` → `h3`).

## Skip Links

```svelte
<a href="#main-content" class="sr-only focus:not-sr-only focus:absolute ...">
    Skip to main content
</a>
```

## Form Accessibility

- **Label every control**: `<label for="email">` or `aria-label` for icon-only inputs
- **Required fields**: Use `required aria-required="true"` with visual `*` (`aria-hidden="true"`)
- **Error messages**: Link via `aria-describedby`, set `aria-invalid={hasError}`
- **On validation failure**: Focus first invalid input
- **Never** disable submit just to block validation

```svelte
<input id="email" aria-invalid={hasError} aria-describedby={hasError ? 'email-error' : undefined} />
{#if hasError}
    <p id="email-error" class="text-destructive">Please enter a valid email address</p>
{/if}
```

## Keyboard Navigation

- Natural tab order follows DOM order — avoid positive `tabindex`
- Use `tabindex="-1"` for hidden/inactive elements
- **Dialogs**: Focus first interactive element on open, return focus to trigger on close
- Interactive non-button elements must handle `Enter` and `Space` key events

## Icon Buttons

Always add `aria-label` and hide the icon from assistive technology:

```svelte
<button aria-label="Delete event">
    <TrashIcon aria-hidden="true" />
</button>
```

When icons accompany visible text, just hide the icon: `<PlusIcon aria-hidden="true" />`.

## ARIA Patterns

**Live regions** for dynamic updates:

```svelte
<div aria-live="polite" aria-atomic="true">{#if loading}Loading events...{/if}</div>
<div role="alert">Error: Failed to save changes</div>
```

**Expandable content**: Use `aria-expanded` and `aria-controls` on the trigger, `hidden` on the panel.

## Color and Contrast

- Minimum contrast ratio: **4.5:1** for normal text, **3:1** for large text and UI components
- Don't rely on color alone — combine with icons or text

```svelte
<!-- ✅ Good: Icon + color + text -->
<span class="text-destructive"><AlertIcon aria-hidden="true" /> Error: Invalid input</span>
```

## Testing

Use `axe-playwright` in E2E tests (`AxeBuilder({ page }).analyze()`) to catch accessibility violations.
