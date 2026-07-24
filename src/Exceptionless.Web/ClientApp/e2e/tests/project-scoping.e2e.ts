import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { getVisibleRow, getVisibleText } from '../support/page-helpers';

test('operator can scope Events to a project and clear the project filter', async ({ e2eApi, e2eScenario, e2eSecondaryProject, page }) => {
    await test.step('seed events in two projects', async () => {
        await Promise.all([
            seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
                message: e2eScenario.message,
                projectId: e2eScenario.projectId,
                projectToken: e2eScenario.projectToken,
                referenceId: e2eScenario.referenceId
            }),
            seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
                message: e2eSecondaryProject.message,
                projectId: e2eSecondaryProject.projectId,
                projectToken: e2eSecondaryProject.projectToken,
                referenceId: e2eSecondaryProject.referenceId
            })
        ]);
    });

    await test.step('show events from both projects before scoping', async () => {
        await page.goto('/next/event?time=all');

        await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eSecondaryProject.message)).toBeVisible({ timeout: 30_000 });
    });

    await test.step('scope to a project from event details', async () => {
        await getVisibleRow(page, e2eScenario.message).click();
        const eventSheet = page.getByRole('dialog', { name: 'Event' });
        await expect(eventSheet).toBeVisible();
        await eventSheet.getByTitle(`Filter project:${e2eScenario.projectId}`).click();

        await expect(page).toHaveURL(new RegExp(`[?&]project=${e2eScenario.projectId}(?:&|$)`));
        await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eSecondaryProject.message)).toBeHidden({ timeout: 30_000 });
    });

    await test.step('persist project scope through reload, then clear it', async () => {
        await page.reload();

        await expect(page.getByRole('button', { name: new RegExp(`^Project\\s+${escapeRegExp(e2eScenario.projectName)}`) })).toBeVisible();
        await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eSecondaryProject.message)).toBeHidden();

        await page.getByRole('button', { name: new RegExp(`^Project\\s+${escapeRegExp(e2eScenario.projectName)}`) }).click();
        await page.getByRole('button', { name: 'Remove filter' }).click();

        await expect(page).not.toHaveURL(/[?&]project=/);
        await expect(getVisibleText(page, e2eSecondaryProject.message)).toBeVisible({ timeout: 30_000 });
    });
});

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
