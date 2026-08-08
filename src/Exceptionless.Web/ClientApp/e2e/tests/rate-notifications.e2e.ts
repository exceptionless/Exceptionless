import { expect, test } from '../fixtures/e2e-test';

test('personal rate notification rule can be created and managed', async ({ e2eApi, e2eScenario, page }) => {
    test.skip(e2eApi.environment.isProduction, 'The rate notification scenario requires a disposable organization and global-admin feature setup.');

    const ruleName = `E2E error cost guardrail ${e2eScenario.run.slice(-32)}`;

    await test.step('enable rate notifications for the disposable project', async () => {
        await e2eApi.changeOrganizationPlan(e2eScenario.userToken, e2eScenario.organizationId, 'EX_UNLIMITED');
        await e2eApi.setOrganizationFeature(e2eScenario.userToken, e2eScenario.organizationId, 'rate-notifications');
        await e2eApi.waitForProjectRateNotificationsEnabled(e2eScenario.userToken, e2eScenario.projectId);
    });

    await test.step('create a rule with actions visible at a standard laptop viewport', async () => {
        await page.setViewportSize({ height: 720, width: 1280 });
        await page.goto(`/next/account/notifications?project=${e2eScenario.projectId}`);

        await expect(page.getByRole('heading', { name: 'Rate Notifications' })).toBeVisible();
        await expect(page.getByText('before they consume your event quota')).toBeVisible();
        await page.getByRole('button', { name: 'Create your first rule' }).click();

        const dialog = page.getByRole('dialog', { name: 'Create Rate Notification Rule' });
        await expect(dialog).toBeVisible();
        await expect(dialog.getByRole('button', { name: 'Create rule' })).toBeInViewport();
        await dialog.getByLabel('Name', { exact: true }).fill(ruleName);
        await dialog.getByRole('button', { name: 'Create rule' }).click();
        await expect(dialog).toBeHidden();

        await expect(page.getByRole('button', { name: ruleName })).toBeVisible();
        await expect(page.getByText('Cooldown 30 minutes')).toBeVisible();
    });

    await test.step('edit, snooze, resume, and disable the rule', async () => {
        await page.getByRole('button', { name: ruleName }).click();

        const dialog = page.getByRole('dialog', { name: 'Edit Rate Notification Rule' });
        await expect(dialog).toBeVisible();
        await dialog.getByLabel('Threshold (events)', { exact: true }).fill('25');
        await dialog.getByRole('button', { name: 'Save changes' }).click();
        await expect(dialog).toBeHidden();
        await expect(page.getByText('≥25 in 5 minutes')).toBeVisible();

        await page.getByRole('button', { name: 'Snooze rule for 1 hour' }).click();
        await expect(page.getByText('Snoozed', { exact: true })).toBeVisible();
        await page.getByRole('button', { name: 'Resume rule' }).click();
        await expect(page.getByText('Snoozed', { exact: true })).toHaveCount(0);

        await page.getByRole('switch', { name: 'Disable rule' }).click();
        await expect(page.getByRole('switch', { name: 'Enable rule' })).toBeVisible();
    });

    await test.step('delete the rule', async () => {
        await page.getByRole('button', { name: 'Delete rule' }).click();

        const dialog = page.getByRole('alertdialog', { name: 'Delete rule?' });
        await expect(dialog).toBeVisible();
        await dialog.getByRole('button', { name: 'Delete' }).click();
        await expect(dialog).toBeHidden();
        await expect(page.getByText('No rate notification rules yet.')).toBeVisible();
    });
});
