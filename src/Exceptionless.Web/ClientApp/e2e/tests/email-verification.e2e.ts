import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

test.skip(process.env.E2E_ENV === 'production', 'Email verification requires local Mailpit.');
test.use({ e2eInjectBrowserToken: false, e2eUseInvitedUser: true });

test('verification email verifies the account @signup', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange
    const verificationLink = await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email');

    // Act
    const currentUser = await e2eApi.getCurrentUser(e2eScenario.userToken);

    // Assert
    expect(currentUser?.is_email_address_verified).toBe(false);

    await test.step('sign in after opening a bad verification link', async () => {
        // Arrange
        const invalidLink = '/account/verify?token=invalidverificationtoken1234567890';

        // Act
        await page.goto(invalidLink);

        // Assert
        await expect(page).toHaveURL(/\/login\?redirect=/);

        // Act
        await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
        await page.getByRole('button', { exact: true, name: 'Login' }).click();

        // Assert
        await expect(page).toHaveURL(/\/account\/manage(?:[?#]|$)/);
        await expect(page.getByText('An error occurred while verifying your account.')).toBeVisible();

        // Act
        const unverifiedUser = await e2eApi.getCurrentUser(e2eScenario.userToken);

        // Assert
        expect(unverifiedUser?.is_email_address_verified).toBe(false);
    });

    await test.step('follow the verification email in another tab', async () => {
        // Arrange
        const confirmationPage = await page.context().newPage();

        // Act
        await confirmationPage.goto(verificationLink);

        // Assert
        await expect(confirmationPage).toHaveURL(/\/account\/manage(?:[?#]|$)/);
        await expect
            .poll(async () => {
                const verifiedUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
                return verifiedUser?.is_email_address_verified;
            })
            .toBe(true);
        await expect(confirmationPage.getByText('Your account has been successfully verified.')).toBeVisible();
        await expect(confirmationPage.getByLabel('Email', { exact: true })).toHaveValue(e2eScenario.email);
        await expect(confirmationPage.getByText('Email not verified.')).toHaveCount(0);

        // Act
        await confirmationPage.reload();

        // Assert
        await expect(confirmationPage.getByLabel('Email', { exact: true })).toHaveValue(e2eScenario.email);
        await expect(confirmationPage.getByText('Email not verified.')).toHaveCount(0);
    });
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

    // Assert
    await expect(page).toHaveURL(/\/login\?redirect=/);

    // Act
    await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
    await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
    await page.getByRole('button', { exact: true, name: 'Login' }).click();

    // Assert
    await expect(page).toHaveURL(/\/account\/manage(?:[?#]|$)/);
    await expect
        .poll(async () => {
            const verifiedUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
            return verifiedUser?.is_email_address_verified;
        })
        .toBe(true);
    await expect(page.getByText('Your account has been successfully verified.')).toBeVisible();
});
