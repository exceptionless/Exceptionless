---
description: "Frontend: Svelte component testing specific guidelines"
applyTo: "src/Exceptionless.Web/ClientApp/**/*.{test,spec}.{ts,js}"
---

# Svelte Component Testing Guidelines

## Testing Library Integration

- Use `@testing-library/svelte` for component testing.
- Import `render`, `screen`, and `fireEvent` from the testing library.
- Use `cleanup` to ensure components are properly unmounted between tests.

## Svelte-Specific Testing Patterns

- Test component props and their effects on rendering.
- Test event dispatching using `createEventDispatcher`.\
- Test component slots and their content projection.
- Validate two-way binding behavior with form inputs.

## State Management Testing

- Test component state changes through user interactions.
- Verify `$state` and `$derived` reactive variables update correctly.
- Test component lifecycle hooks if they affect behavior.
- Mock store values and test component reactions to store changes.

## Accessibility Testing

- Ensure proper ARIA attributes are present and correct.
- Test keyboard navigation and focus management.
- Verify screen reader compatibility with semantic HTML.
- Test color contrast and visual accessibility requirements.
