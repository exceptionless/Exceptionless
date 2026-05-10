import { expect, test } from '../fixtures/e2e-test';

test('api health endpoint responds', async ({ e2eApi }) => {
    const about = await e2eApi.getAbout();

    expect(about).toBeTruthy();
});

test('default app route redirects anonymous users to login', async ({ page }) => {
    await page.goto('/next');

    await page.waitForURL('**/next/login');
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible();
});
