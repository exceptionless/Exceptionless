import { expect, test } from '@playwright/test';

test('default route should redirect to login page when unauthorized', async ({ page }) => {
    await page.goto('/next');
    await page.waitForURL('/next/login');
    await expect(page.getByRole('button', { name: 'Login' })).toBeVisible();
});
