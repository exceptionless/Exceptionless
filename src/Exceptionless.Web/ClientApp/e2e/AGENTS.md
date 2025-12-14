# E2E Testing Guidelines (Playwright)

Applies to Playwright tests in `src/Exceptionless.Web/ClientApp/e2e`. For general frontend testing guidance, see [ClientApp/AGENTS.md](../AGENTS.md#testing).

## Running Tests

```bash
npx playwright install  # first time only
npm run test:e2e
```

## Page Object Model

- Create page objects for reusable page interactions
- Group tests by feature or user workflow in separate files
- Use descriptive test names that explain the user scenario
- Keep tests focused on end-to-end user journeys

## Selectors

1. **Semantic selectors first:** `page.getByRole()`, `page.getByLabel()`, `page.getByText()`
2. **Fallback:** `data-testid` attributes when semantic options aren't available
3. **Avoid:** CSS selectors tied to implementation details

## Timing & Assertions

- Use `page.waitForLoadState()` for navigation
- Use `expect().toBeVisible()` before interactions
- Implement meaningful assertions that validate user-visible outcomes

## Test Data

- Use test fixtures for consistent setup
- Clean up test data after each test run
- Use unique identifiers to avoid test interference

## Cross-Browser & Accessibility

- Configure tests for Chrome, Firefox, and Safari
- Test responsive breakpoints and mobile interactions
- Include `axe-playwright` for accessibility audits
