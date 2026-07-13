import { expect, test } from '../fixtures/e2e-test';

test('user can recover from a failed login and keep the authenticated session after reload', async ({ e2eApi, page }) => {
    const { email, password } = e2eApi.environment;
    test.skip(!email || !password, 'UI login requires E2E_EMAIL and E2E_PASSWORD.');

    await test.step('show an actionable error for invalid credentials', async () => {
        await page.goto('/next/login');
        await page.getByLabel('Email', { exact: true }).fill(email!);
        await page.getByPlaceholder('Enter password').fill(`${password!}-invalid`);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();

        await expect(page.getByText('Invalid email or password', { exact: true })).toBeVisible();
        await expect(page).toHaveURL(/\/next\/login(?:[?#]|$)/);
    });

    await test.step('log in through the form', async () => {
        await page.getByPlaceholder('Enter password').fill(password!);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();

        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(/\/next\/stack(?:[?#]|$)/);
    });

    await test.step('restore the authenticated application after a reload', async () => {
        await page.reload();

        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(/\/next\/stack(?:[?#]|$)/);
    });
});
