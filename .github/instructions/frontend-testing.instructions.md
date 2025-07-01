---
description: "Frontend: TypeScript/JavaScript testing guidelines"
applyTo: "src/Exceptionless.Web/ClientApp/**/*.{test,spec}.{ts,js}"
---

# Frontend Testing Guidelines (TypeScript/JavaScript)

## Testing Framework Stack

- **Unit Testing**: Vitest
- **Component Testing**: Testing Library (@testing-library/svelte)
- **E2E Testing**: Playwright
- **Mocking**: vi.mock() from Vitest

## Unit Testing Best Practices

- Write comprehensive tests for all utility functions and business logic.
- Use descriptive test names: `describe` and `it` blocks should read like sentences.
- Follow AAA pattern (Arrange, Act, Assert).
- Test both happy paths and error conditions.
- Mock external dependencies and API calls.

## Component Testing

- Test component behavior, not implementation details.
- Focus on user interactions and visible outcomes.
- Use `render` from Testing Library to mount components.
- Query elements by role, label, or text content (not by class names or IDs).
- Test accessibility features and keyboard navigation.

## E2E Testing with Playwright

- Write E2E tests for critical user workflows.
- Use page object model pattern for better maintainability.
- Test across different browsers and viewports.
- Include visual regression testing where appropriate.

## Test Organization

- Co-locate test files with the code they test using `.test.ts` or `.spec.ts` extensions.
- Use `describe` blocks to group related tests.
- Keep tests isolated and independent of each other.
- Use `beforeEach` and `afterEach` for setup and cleanup.
