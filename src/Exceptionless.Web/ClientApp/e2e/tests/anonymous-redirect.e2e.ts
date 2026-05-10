import { expect, test } from '../fixtures/e2e-test';

test('default app route redirects anonymous users to login', async ({ page }) => {
    await page.goto('/next');

    await page.waitForURL('**/next/login');
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible();
});
