import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

test.use({ e2eUseGeneratedUser: true });

test('user can recover from a failed login, restore the session, and log out @signup', async ({ browser, e2eApi, e2eScenario, page }) => {
    await test.step('show an actionable error for invalid credentials', async () => {
        await page.goto('/next/login');
        await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await page.getByPlaceholder('Enter password').fill(`${E2E_TEST_PASSWORD}-invalid`);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();

        await expect(page.getByText('Invalid email or password', { exact: true })).toBeVisible();
        await expect(page).toHaveURL(/\/next\/login(?:[?#]|$)/);
    });

    await test.step('log in through the form', async () => {
        await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();

        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(/\/next\/stack(?:[?#]|$)/);
    });

    await test.step('restore the authenticated application after a reload', async () => {
        await page.reload();

        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(/\/next\/stack(?:[?#]|$)/);
    });

    await test.step('log out through the user menu', async () => {
        await page.getByRole('button').filter({ hasText: e2eScenario.email }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { exact: true, name: 'Log Out' }).click();

        await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
        await expect(page).toHaveURL(/\/next\/login(?:[?#]|$)/);
    });

    await test.step('redirect a signed-out user away from a protected route', async () => {
        const signedOutContext = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true });
        const signedOutPage = await signedOutContext.newPage();

        try {
            await signedOutPage.goto('/next/stack');

            await expect(signedOutPage.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
            await expect(signedOutPage).toHaveURL(/\/next\/login(?:[?#]|$)/);
        } finally {
            await signedOutContext.close();
        }
    });
});
