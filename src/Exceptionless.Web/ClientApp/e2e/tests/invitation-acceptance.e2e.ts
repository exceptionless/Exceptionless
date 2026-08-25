import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';
import { runCleanupStep, throwIfCleanupFailed } from '../support/cleanup';
import { getUserToken, waitForEmailValidation } from '../support/page-helpers';

test.skip(process.env.E2E_ENV === 'production', 'Invitation acceptance requires local Mailpit.');

test('invited user can accept an organization invitation @signup', async ({ browser, e2eApi, e2eScenario, page }) => {
    const invitedEmail = `invited-${e2eScenario.run}@exceptionless.test`.toLowerCase();
    let invitedUserSignupAttempted = false;
    let invitedUserToken: string | undefined;

    try {
        await test.step('invite a user through organization settings', async () => {
            await page.goto(`/next/organization/${e2eScenario.organizationId}/users`);
            await page.getByTitle('Invite User').click();

            const dialog = page.getByRole('alertdialog', { name: 'Invite User' });
            await dialog.getByLabel('Email Address').fill(invitedEmail);
            await dialog.getByRole('button', { name: 'Invite User' }).click();

            await expect(page.getByText('User invited successfully')).toBeVisible();
        });

        const inviteToken = await test.step('read the invitation from local mail', async () => {
            return await e2eApi.pollForMailToken(invitedEmail, 'signup');
        });

        await test.step('sign up through the invitation route', async () => {
            const invitedContext = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true });
            const invitedPage = await invitedContext.newPage();

            try {
                await invitedPage.goto(`/next/signup?token=${encodeURIComponent(inviteToken)}`);
                await invitedPage.getByLabel('Name', { exact: true }).fill(`Invited User ${e2eScenario.run}`);
                await invitedPage.getByLabel('Email', { exact: true }).fill(invitedEmail);
                await waitForEmailValidation(invitedPage);
                await invitedPage.getByLabel('Password', { exact: true }).fill(E2E_TEST_PASSWORD);

                const signupResponse = invitedPage.waitForResponse((response) => {
                    const url = new URL(response.url());
                    return response.request().method() === 'POST' && url.pathname.endsWith('/api/v2/auth/signup');
                });

                invitedUserSignupAttempted = true;
                await invitedPage.getByRole('button', { name: 'Create My Account' }).click();
                expect((await signupResponse).ok()).toBe(true);

                invitedUserToken = await getUserToken(invitedPage);
                await expect(invitedPage).toHaveURL(/\/next\/project\/add(?:[?#]|$)/, { timeout: 30_000 });
                await e2eApi.waitForOrganizationListed(invitedUserToken, e2eScenario.organizationId, 60_000);
                await invitedPage.reload();
                await expect(invitedPage.getByRole('heading', { name: 'Add Project' })).toBeVisible();
                await expect(invitedPage.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first()).toBeVisible();
            } finally {
                await invitedContext.close();
            }
        });
    } finally {
        const cleanupErrors: Error[] = [];

        if (!invitedUserToken && invitedUserSignupAttempted) {
            await runCleanupStep(cleanupErrors, 'restore invited user session for cleanup', async () => {
                invitedUserToken = await e2eApi.loginIfExists(invitedEmail, E2E_TEST_PASSWORD);
            });
        }

        if (invitedUserToken) {
            const token = invitedUserToken;

            await runCleanupStep(cleanupErrors, 'remove invited user from organization', async () => {
                await e2eApi.deleteOrganizationUser(e2eScenario.userToken, e2eScenario.organizationId, invitedEmail);
                await e2eApi.waitForOrganizationNotListed(token, e2eScenario.organizationId);
            });

            await runCleanupStep(cleanupErrors, 'delete invited user', async () => {
                await e2eApi.deleteCurrentUser(token);
                await e2eApi.waitForCurrentUserDeleted(token);
            });
        }

        throwIfCleanupFailed(cleanupErrors);
    }
});

test('existing invited user can accept an organization invitation when logging in @signup', async ({ browser, e2eApi, e2eScenario }) => {
    const invitedEmail = `existing-invited-${e2eScenario.run}@exceptionless.test`.toLowerCase();
    let invitedUserToken: string | undefined;

    try {
        await test.step('create an invitation for a new address', async () => {
            await e2eApi.inviteOrganizationUser(e2eScenario.userToken, e2eScenario.organizationId, invitedEmail);
        });

        const inviteToken = await e2eApi.pollForMailToken(invitedEmail, 'signup');
        invitedUserToken = await e2eApi.signup(`Existing Invited User ${e2eScenario.run}`, invitedEmail, E2E_TEST_PASSWORD);
        const invitedContext = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true });
        const invitedPage = await invitedContext.newPage();

        try {
            await invitedPage.goto(`/next/login?token=${encodeURIComponent(inviteToken)}`);
            await invitedPage.getByLabel('Email', { exact: true }).fill(invitedEmail);
            await invitedPage.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);

            const loginResponse = invitedPage.waitForResponse((response) => {
                const url = new URL(response.url());
                return response.request().method() === 'POST' && url.pathname.endsWith('/api/v2/auth/login');
            });

            await invitedPage.getByRole('button', { exact: true, name: 'Login' }).click();
            const response = await loginResponse;
            const requestBody = response.request().postDataJSON() as { invite_token?: string };
            expect(requestBody.invite_token).toBe(inviteToken);
            expect(response.ok()).toBe(true);

            invitedUserToken = await getUserToken(invitedPage);
            await e2eApi.waitForOrganizationListed(invitedUserToken, e2eScenario.organizationId, 60_000);
            await expect(invitedPage.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first()).toBeVisible();
        } finally {
            await invitedContext.close();
        }
    } finally {
        const cleanupErrors: Error[] = [];

        if (invitedUserToken) {
            const token = invitedUserToken;

            await runCleanupStep(cleanupErrors, 'remove existing invited user from organization', async () => {
                await e2eApi.deleteOrganizationUser(e2eScenario.userToken, e2eScenario.organizationId, invitedEmail);
                await e2eApi.waitForOrganizationNotListed(token, e2eScenario.organizationId);
            });

            await runCleanupStep(cleanupErrors, 'delete existing invited user', async () => {
                await e2eApi.deleteCurrentUser(token);
                await e2eApi.waitForCurrentUserDeleted(token);
            });
        }

        throwIfCleanupFailed(cleanupErrors);
    }
});
