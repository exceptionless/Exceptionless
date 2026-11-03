import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

test.skip(process.env.E2E_ENV === 'production', 'Email verification requires local Mailpit.');
test.use({ e2eInjectBrowserToken: false, e2eUseInvitedUser: true });

test('verification email verifies the account @signup', async ({ e2eApi, e2eScenario, page }) => {
    const verificationLink = await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email');
    const currentUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
    expect(currentUser?.is_email_address_verified).toBe(false);

    await test.step('sign in after opening a bad verification link', async () => {
        await page.goto('/account/verify?token=invalidverificationtoken1234567890');
        await expect(page).toHaveURL(/\/login\?redirect=/);
        await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();

        await expect(page).toHaveURL(/\/account\/manage(?:[?#]|$)/);
        await expect(page.getByText('An error occurred while verifying your account.')).toBeVisible();
        expect((await e2eApi.getCurrentUser(e2eScenario.userToken))?.is_email_address_verified).toBe(false);
    });

    await test.step('follow the verification email in another tab', async () => {
        const confirmationPage = await page.context().newPage();
        await confirmationPage.goto(verificationLink);

        await expect(confirmationPage).toHaveURL(/\/account\/manage(?:[?#]|$)/);
        await expect.poll(async () => (await e2eApi.getCurrentUser(e2eScenario.userToken))?.is_email_address_verified).toBe(true);
        await expect(confirmationPage.getByText('Your account has been successfully verified.')).toBeVisible();
        await expect(confirmationPage.getByLabel('Email', { exact: true })).toHaveValue(e2eScenario.email);
        await expect(confirmationPage.getByText('Email not verified.')).toHaveCount(0);

        await confirmationPage.reload();
        await expect(confirmationPage.getByLabel('Email', { exact: true })).toHaveValue(e2eScenario.email);
        await expect(confirmationPage.getByText('Email not verified.')).toHaveCount(0);
    });
});

test('a legacy verification link survives the login redirect @signup', async ({ e2eApi, e2eScenario, page }) => {
    const verificationLink = new URL(await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email'));
    verificationLink.hash = `!${verificationLink.pathname}${verificationLink.search}`;
    verificationLink.pathname = '/';
    verificationLink.search = '';

    await page.goto(verificationLink.toString());
    await expect(page).toHaveURL(/\/login\?redirect=/);
    await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
    await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
    await page.getByRole('button', { exact: true, name: 'Login' }).click();

    await expect(page).toHaveURL(/\/account\/manage(?:[?#]|$)/);
    await expect.poll(async () => (await e2eApi.getCurrentUser(e2eScenario.userToken))?.is_email_address_verified).toBe(true);
    await expect(page.getByText('Your account has been successfully verified.')).toBeVisible();
});
