import { expect, test } from '../fixtures/e2e-test';

test.skip(process.env.E2E_ENV === 'production', 'Email verification requires local Mailpit.');
test.use({ e2eUseGeneratedUser: true });

test('new user can verify their email address from the welcome email @signup', async ({ e2eApi, e2eScenario, page }) => {
    const verifyLink = await test.step('read the verification link from local mail', async () => {
        expect((await e2eApi.getCurrentUser(e2eScenario.userToken))?.is_email_address_verified).toBe(false);
        return await e2eApi.pollForMailLink(e2eScenario.email, 'account/verify');
    });

    await test.step('open the emailed link and continue into the app', async () => {
        await page.goto(verifyLink);

        await expect(page.getByText('Your email address is verified.')).toBeVisible({ timeout: 30_000 });
        await expect(page).not.toHaveURL(/\/account\/(?:manage|verify)(?:[?#]|$)/);
        expect((await e2eApi.getCurrentUser(e2eScenario.userToken))?.is_email_address_verified).toBe(true);
    });

    await test.step('reopen the used link without an error', async () => {
        await page.goto(verifyLink);

        await expect(page).toHaveURL(/\/account\/manage(?:[?#]|$)/);
        await expect(page.getByText('Email not verified.')).toBeHidden();
    });
});
