import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

const RESET_PASSWORD = `${E2E_TEST_PASSWORD}-reset`;

test.skip(process.env.E2E_ENV === 'production', 'Password recovery requires local Mailpit.');
test.use({ e2eCleanupPassword: RESET_PASSWORD, e2eUseGeneratedUser: true });

test('user can reset a forgotten password and log in @signup', async ({ e2eApi, e2eScenario, page }) => {
    await test.step('request a password reset through the UI', async () => {
        await page.goto('/next/forgot-password');
        await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await page.getByRole('button', { name: 'Send Reset Email' }).click();

        await expect(page).toHaveURL(/\/next\/login(?:[?#]|$)/);
        await expect(page.getByText('Please check your inbox for the password reset email.')).toBeVisible();
    });

    const resetToken = await test.step('read the reset link from local mail', async () => {
        return await e2eApi.pollForMailToken(e2eScenario.email, 'reset-password');
    });

    await test.step('change the password through the emailed route', async () => {
        await page.goto(`/next/reset-password/${encodeURIComponent(resetToken)}`);
        await page.getByLabel('New Password', { exact: true }).fill(RESET_PASSWORD);
        await page.getByLabel('Confirm Password', { exact: true }).fill(RESET_PASSWORD);
        await page.getByRole('button', { name: 'Change Password' }).click();

        await expect(page).toHaveURL(/\/next\/login(?:[?#]|$)/);
        await expect(page.getByText('You have successfully changed your password.')).toBeVisible();
    });

    await test.step('log in with the new password', async () => {
        await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await page.getByPlaceholder('Enter password').fill(RESET_PASSWORD);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();

        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
    });
});
