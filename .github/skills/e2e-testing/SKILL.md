---
name: E2E Testing (Frontend)
description: |
  End-to-end frontend testing with Playwright. Page Object Model, selectors, fixtures,
  accessibility audits. Limited E2E coverage currently - area for improvement.
  Keywords: Playwright, E2E, Page Object Model, POM, data-testid, getByRole, getByLabel,
  getByText, fixtures, axe-playwright, frontend testing
---

# E2E Testing (Frontend)

> **Note:** E2E test coverage is currently limited. This is an area for improvement.

## Running Tests

```bash
npx playwright install  # First time only
npm run test:e2e
```

## Page Object Model

Create page objects for reusable page interactions:

```typescript
// e2e/pages/login-page.ts
import { type Page, type Locator, expect } from '@playwright/test';

export class LoginPage {
    readonly page: Page;
    readonly emailInput: Locator;
    readonly passwordInput: Locator;
    readonly submitButton: Locator;
    readonly errorMessage: Locator;

    constructor(page: Page) {
        this.page = page;
        this.emailInput = page.getByLabel('Email');
        this.passwordInput = page.getByLabel('Password');
        this.submitButton = page.getByRole('button', { name: /log in/i });
        this.errorMessage = page.getByRole('alert');
    }

    async goto() {
        await this.page.goto('/login');
    }

    async login(email: string, password: string) {
        await this.emailInput.fill(email);
        await this.passwordInput.fill(password);
        await this.submitButton.click();
    }

    async expectError(message: string) {
        await expect(this.errorMessage).toContainText(message);
    }
}
```

## Using Page Objects in Tests

```typescript
// e2e/auth/login.spec.ts
import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/login-page';

test.describe('Login', () => {
    test('successful login redirects to dashboard', async ({ page }) => {
        const loginPage = new LoginPage(page);

        await loginPage.goto();
        await loginPage.login('user@example.com', 'password123');

        await expect(page).toHaveURL('/');
    });

    test('invalid credentials shows error', async ({ page }) => {
        const loginPage = new LoginPage(page);

        await loginPage.goto();
        await loginPage.login('wrong@example.com', 'wrongpassword');

        await loginPage.expectError('Invalid email or password');
    });
});
```

## Selector Priority

1. **Semantic selectors first**:

   ```typescript
   page.getByRole('button', { name: /submit/i });
   page.getByLabel('Email address');
   page.getByText('Welcome back');
   ```

2. **Fallback to test IDs**:

   ```typescript
   page.getByTestId('stack-trace');
   ```

3. **Avoid implementation details**:

   ```typescript
   // ❌ Avoid CSS classes and IDs
   page.locator('.btn-primary');
   ```

## Backend Data Setup

E2E tests run against the full Aspire stack. The backend uses the same `AppWebHostFactory` infrastructure from [backend-testing](backend-testing/SKILL.md).

For tests requiring specific data, consider:

1. **API calls in beforeEach** — Use Playwright's request context to set up data
2. **Test-specific endpoints** — Create `/api/test/*` endpoints for test data management
3. **Database seeding** — Seed required data before test runs
4. **Aspire orchestration** — Tests start with Elasticsearch and Redis pre-configured

```typescript
test.beforeEach(async ({ request }) => {
    // Set up test data via API
    await request.post('/api/test/seed', {
        data: { scenario: 'events-with-errors' }
    });
});

test.afterEach(async ({ request }) => {
    await request.delete('/api/test/cleanup');
});
```

**Note:** Backend services use in-memory implementations during tests. See `AppWebHostFactory` for how test infrastructure is configured.

## Accessibility Audits

```typescript
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('login page has no accessibility violations', async ({ page }) => {
    await page.goto('/login');

    const results = await new AxeBuilder({ page }).analyze();
    expect(results.violations).toEqual([]);
});
```

See [accessibility](accessibility/SKILL.md) for WCAG guidelines.
