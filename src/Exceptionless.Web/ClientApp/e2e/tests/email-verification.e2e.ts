import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

test.skip(process.env.E2E_ENV === 'production', 'Email verification requires local Mailpit.');
test.use({ e2eInjectBrowserToken: false, e2eUseInvitedUser: true });

test('an invalid verification link preserves the unverified account @signup', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange
    const invalidLink = '/account/verify?token=invalidverificationtoken1234567890';

    // Act
    await page.goto(invalidLink);
    await page.waitForURL(/\/login\?redirect=/);
    await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
    await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
    await page.getByRole('button', { exact: true, name: 'Login' }).click();
    await page.waitForURL(/\/account\/manage(?:[?#]|$)/);
    const user = await e2eApi.getCurrentUser(e2eScenario.userToken);

    // Assert
    expect(user?.is_email_address_verified).toBe(false);
    await expect(page.getByText('An error occurred while verifying your account.')).toBeVisible();
});

test('verification in another tab refreshes the original account page @signup', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange
    const verificationLink = await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email');
    const initialUser = await e2eApi.getCurrentUser(e2eScenario.userToken);

    // Act
    await page.goto('/login?redirect=%2Faccount%2Fmanage');
    await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
    await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
    await page.getByRole('button', { exact: true, name: 'Login' }).click();
    await page.waitForURL(/\/account\/manage(?:[?#]|$)/);
    const confirmationPage = await page.context().newPage();
    await confirmationPage.goto(verificationLink);
    await confirmationPage.waitForURL(/\/account\/manage(?:[?#]|$)/);
    await confirmationPage.getByText(`Verified ${e2eScenario.email}.`).waitFor();
    const verifiedUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
    await confirmationPage.reload();
    await page.bringToFront();

    // Assert
    expect(initialUser?.is_email_address_verified).toBe(false);
    expect(verifiedUser?.is_email_address_verified).toBe(true);
    await expect(confirmationPage.getByLabel('Email', { exact: true })).toHaveValue(e2eScenario.email);
    await expect(confirmationPage.getByText('Email not verified.')).toHaveCount(0);
    await expect(page.getByText('Email not verified.')).toHaveCount(0);
});

test('a link for another account leaves the signed-in account able to resend verification @signup', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange
    const otherEmail = `verify-other-${e2eScenario.run}@exceptionless.test`.toLowerCase();
    let otherUserToken: string | undefined;

    try {
        otherUserToken = await e2eApi.signup('Other verification user', otherEmail, E2E_TEST_PASSWORD);
        const verificationLink = await e2eApi.pollForMailLink(otherEmail, 'verify-email');
        const originalSignedInUserLink = await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email');

        // Act
        await page.goto('/login?redirect=%2Faccount%2Fmanage');
        await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();
        await page.waitForURL(/\/account\/manage(?:[?#]|$)/);
        await page.goto(verificationLink);
        await page.waitForURL(/\/account\/manage(?:[?#]|$)/);
        const verifiedMessage = await page.getByText(`Verified ${otherEmail}.`).textContent();
        await page.getByText('Resend', { exact: true }).click();
        const signedInUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
        const otherUser = await e2eApi.getCurrentUser(otherUserToken);

        // Assert
        expect(verifiedMessage).toBe(`Verified ${otherEmail}.`);
        expect(signedInUser?.is_email_address_verified).toBe(false);
        expect(otherUser?.is_email_address_verified).toBe(true);
        await expect(page.getByLabel('Email', { exact: true })).toHaveValue(e2eScenario.email);
        await expect(page.getByText('Email not verified.')).toBeVisible();
        await expect(page.getByText('Please check your inbox for the verification email.')).toBeVisible();
        await expect.poll(() => e2eApi.pollForMailLink(e2eScenario.email, 'verify-email'), { timeout: 30_000 }).not.toBe(originalSignedInUserLink);

        // Act
        const newVerificationLink = await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email');
        await page.goto(newVerificationLink);
        await page.waitForURL(/\/account\/manage(?:[?#]|$)/);
        const verifiedSignedInUser = await e2eApi.getCurrentUser(e2eScenario.userToken);

        // Assert
        expect(verifiedSignedInUser?.is_email_address_verified).toBe(true);
        await expect(page.getByText('Email not verified.')).toHaveCount(0);
        await expect(page.getByText(`Verified ${e2eScenario.email}.`)).toBeVisible();
    } finally {
        if (otherUserToken) {
            await e2eApi.deleteCurrentUser(otherUserToken);
            await e2eApi.waitForCurrentUserDeleted(otherUserToken);
        }
    }
});

test('a legacy verification link survives the login redirect @signup', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange
    const deliveredLink = await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email');
    const verificationLink = new URL(deliveredLink);
    verificationLink.hash = `!${verificationLink.pathname}${verificationLink.search}`;
    verificationLink.pathname = '/';
    verificationLink.search = '';

    // Act
    await page.goto(verificationLink.toString());
    await page.waitForURL(/\/login\?redirect=/);
    await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
    await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
    await page.getByRole('button', { exact: true, name: 'Login' }).click();
    await page.waitForURL(/\/account\/manage(?:[?#]|$)/);
    const user = await e2eApi.getCurrentUser(e2eScenario.userToken);

    // Assert
    expect(user?.is_email_address_verified).toBe(true);
    await expect(page.getByText(`Verified ${e2eScenario.email}.`)).toBeVisible();
});
