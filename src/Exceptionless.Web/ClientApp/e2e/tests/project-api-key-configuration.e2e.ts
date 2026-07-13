import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { getVisibleText } from '../support/page-helpers';

test('operator can manage project API keys and follow first-event redirect', async ({ e2eApi, e2eScenario, page }) => {
    await test.step('open API Keys and create another client key', async () => {
        await page.goto(`/next/project/${e2eScenario.projectId}/api-keys`);

        await expect(page.getByRole('heading', { name: `${e2eScenario.projectName} Settings` })).toBeVisible();
        await expect(page.getByRole('cell', { name: e2eScenario.projectToken })).toBeVisible();
        await page.getByTitle('Add API Key').click();
        await expect(page.getByText('API Key added successfully')).toBeVisible();
        await expect(page.getByRole('row')).toHaveCount(3);
    });

    await test.step('open client setup from API Keys', async () => {
        await page.locator(`a[href="/next/project/${e2eScenario.projectId}/configure"]`).filter({ hasText: 'Client setup' }).last().click();

        await expect(page).toHaveURL(new RegExp(`/next/project/${e2eScenario.projectId}/configure(?:[?#]|$)`));
        await expect(page.getByRole('heading', { name: `Send Events to ${e2eScenario.projectName}` })).toBeVisible();
    });

    await test.step('redirect to project Events when the first event arrives', async () => {
        await page.goto(`/next/project/${e2eScenario.projectId}/configure?redirect=true&type=bash`);
        await expect(page.getByText('Waiting for your first event')).toBeVisible();
        await expect(page.getByText(e2eScenario.projectToken, { exact: false }).first()).toBeVisible();

        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: e2eScenario.message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: e2eScenario.referenceId
        });

        await expect(page).toHaveURL(new RegExp(`/next/event[?].*project=${e2eScenario.projectId}`), { timeout: 30_000 });
        await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
    });
});
