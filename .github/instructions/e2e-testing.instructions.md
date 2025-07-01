---
description: "E2E testing guidelines for Playwright tests"
applyTo: "src/Exceptionless.Web/ClientApp/e2e/**/*.{test,spec}.{ts,js}"
---

# E2E Testing Guidelines (Playwright)

## Test Structure & Organization

- Use Page Object Model pattern for reusable page interactions.
- Group tests by feature or user workflow in separate files.
- Use descriptive test names that explain the user scenario.
- Keep tests focused on end-to-end user journeys.

## Playwright Best Practices

- Use `page.locator()` with semantic selectors (roles, labels, text).
- Prefer `data-testid` attributes over CSS selectors when semantic options aren't available.
- Use `page.waitForLoadState()` and `expect().toBeVisible()` for reliable timing.
- Implement proper error handling and meaningful assertions.

## Test Data Management

- Use test fixtures for consistent test data setup.
- Clean up test data after each test run.
- Use unique identifiers to avoid test interference.
- Consider using database snapshots for complex scenarios.

## Cross-Browser & Responsive Testing

- Configure tests to run across Chrome, Firefox, and Safari.
- Test responsive breakpoints and mobile interactions.
- Verify touch gestures and mobile-specific behaviors.
- Include accessibility testing with axe-playwright.

## Visual & Performance Testing

- Use `page.screenshot()` for visual regression testing.
- Implement performance assertions for critical user paths.
- Monitor Core Web Vitals metrics during testing.
- Test offline scenarios and network conditions where applicable.
