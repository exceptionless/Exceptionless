import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

test.use({ e2eUseGeneratedUser: true });

test('user can recover from a failed login, restore the session, and log out', async ({ browser, e2eApi, e2eScenario }) => {
    const authenticationContext = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true });
    const page = await authenticationContext.newPage();

    try {
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

            await expect(page.getByRole('heading', { name: 'All' })).toBeVisible({ timeout: 30_000 });
            await expect(page).toHaveURL(/\/next\/stack\/all(?:[?#]|$)/);
        });

        await test.step('restore the authenticated application after a reload', async () => {
            await page.reload();

            await expect(page.getByRole('heading', { name: 'All' })).toBeVisible({ timeout: 30_000 });
            await expect(page).toHaveURL(/\/next\/stack\/all(?:[?#]|$)/);
        });

        await test.step('log out through the user menu', async () => {
            await page.getByRole('button').filter({ hasText: e2eScenario.email }).filter({ visible: true }).first().click();
            await page.getByRole('menuitem', { exact: true, name: 'Log Out' }).click();

            await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
            await expect(page).toHaveURL(/\/next\/login(?:[?#]|$)/);
        });

        await test.step('redirect a signed-out user away from a protected route', async () => {
            await page.goto('/next/stack');

            await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
            await expect(page).toHaveURL(/\/next\/login(?:[?#]|$)/);
        });
    } finally {
        await authenticationContext.close();
    }
});

test('login restores the full notification settings link and selected project', async ({ browser, e2eApi, e2eScenario, e2eSecondaryProject }) => {
    const context = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true });
    const page = await context.newPage();
    const destination = `/next/account/notifications?project=${e2eSecondaryProject.projectId}&from=email%2Bnotification%26settings#project-notifications`;

    try {
        await test.step('preserve the complete destination when authentication is required', async () => {
            await page.goto(destination);
            await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
            await expect.poll(() => new URL(page.url()).searchParams.get('redirect')).toBe(destination);
        });

        await test.step('return to the requested project after login', async () => {
            await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
            await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
            await page.getByRole('button', { exact: true, name: 'Login' }).click();

            await expect(page).toHaveURL(new URL(destination, e2eApi.environment.appUrl).href);
            await expect(page.getByRole('heading', { exact: true, name: 'Project Notifications' })).toBeVisible();
            await expect(page.getByRole('button', { exact: true, name: e2eSecondaryProject.projectName })).toBeVisible();
        });

        await test.step('preserve the same destination after the session expires', async () => {
            await page.evaluate(() => localStorage.setItem('satellizer_token', 'expired-navigation-test-token'));
            await page.reload();

            await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
            await expect.poll(() => new URL(page.url()).searchParams.get('redirect')).toBe(destination);
        });
    } finally {
        await context.close();
    }
});
