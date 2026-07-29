import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';
import { getUserToken, waitForEmailValidation } from '../support/page-helpers';

test.skip(process.env.E2E_ENV === 'production', 'Invitation acceptance requires local Mailpit.');

test('invited user can accept an organization invitation @signup', async ({ browser, e2eApi, e2eScenario, page }) => {
    const invitedEmail = `invited-${e2eScenario.run}@exceptionless.test`.toLowerCase();
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
                await invitedPage.getByRole('button', { name: 'Create My Account' }).click();

                invitedUserToken = await getUserToken(invitedPage);
                await expect(invitedPage).toHaveURL(/\/next\/project\/add(?:[?#]|$)/, { timeout: 30_000 });
                await expect(invitedPage.getByRole('heading', { name: 'Add Project' })).toBeVisible();
                await expect(invitedPage.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first()).toBeVisible();
            } finally {
                await invitedContext.close();
            }
        });
    } finally {
        if (invitedUserToken) {
            await e2eApi.deleteOrganizationUser(e2eScenario.userToken, e2eScenario.organizationId, invitedEmail);
            await e2eApi.waitForOrganizationNotListed(invitedUserToken, e2eScenario.organizationId);
            await e2eApi.deleteCurrentUser(invitedUserToken);
            await e2eApi.waitForCurrentUserDeleted(invitedUserToken);
        }
    }
});
