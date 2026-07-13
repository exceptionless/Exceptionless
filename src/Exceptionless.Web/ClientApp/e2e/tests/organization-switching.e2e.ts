import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { getVisibleText } from '../support/page-helpers';

test('operator can switch organizations without leaking event data', async ({ e2eApi, e2eScenario, e2eSecondaryOrganization, page }) => {
    await test.step('seed a distinct event in each organization', async () => {
        await Promise.all([
            seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
                message: e2eScenario.message,
                projectId: e2eScenario.projectId,
                projectToken: e2eScenario.projectToken,
                referenceId: e2eScenario.referenceId
            }),
            seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
                message: e2eSecondaryOrganization.message,
                projectId: e2eSecondaryOrganization.projectId,
                projectToken: e2eSecondaryOrganization.projectToken,
                referenceId: e2eSecondaryOrganization.referenceId
            })
        ]);
    });

    await test.step('show only the active organization data', async () => {
        await page.goto('/next/event?time=all');

        await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eSecondaryOrganization.message)).toBeHidden();
    });

    await test.step('switch organizations through the sidebar and persist the selection', async () => {
        await page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { exact: true, name: e2eSecondaryOrganization.organizationName }).click();

        await expect(getVisibleText(page, e2eSecondaryOrganization.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eScenario.message)).toBeHidden();

        await page.reload();

        await expect(page.getByRole('button').filter({ hasText: e2eSecondaryOrganization.organizationName }).filter({ visible: true }).first()).toBeVisible();
        await expect(getVisibleText(page, e2eSecondaryOrganization.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eScenario.message)).toBeHidden();
    });
});
