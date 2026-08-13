import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { escapeRegExp, getVisibleRow, getVisibleText } from '../support/page-helpers';

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

        await page.goBack();
        await expect(page).toHaveURL(new RegExp(`[?&]project=${e2eScenario.projectId}(?:&|$)`));
        await expect(page.getByRole('button', { name: new RegExp(`^Project\\s+${escapeRegExp(e2eScenario.projectName)}`) })).toBeVisible();
        await expect(getVisibleText(page, e2eSecondaryProject.message)).toBeHidden({ timeout: 30_000 });

        await page.goForward();
        await expect(page).not.toHaveURL(/[?&]project=/);
        await expect(getVisibleText(page, e2eSecondaryProject.message)).toBeVisible({ timeout: 30_000 });
    });
});

test('project scope on Most Frequent Errors survives immediate reload and history traversal', async ({ e2eApi, e2eScenario, page }) => {
    await test.step('seed an error and scope the stack list from its details', async () => {
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: e2eScenario.message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: e2eScenario.referenceId
        });

        await page.goto('/next/stack/most-frequent-errors');
        const stackRow = getVisibleRow(page, e2eScenario.message);
        await expect(stackRow).toBeVisible({ timeout: 30_000 });
        await stackRow.click();

        const stackSheet = page.getByRole('dialog', { name: 'Stack' });
        await expect(stackSheet).toBeVisible();
        await stackSheet.getByTitle(`Filter project:${e2eScenario.projectId}`).click();
    });

    await test.step('retain scope through an immediate reload', async () => {
        await page.reload();

        await expect(page).toHaveURL(new RegExp(`[?&]project=${e2eScenario.projectId}(?:&|$)`));
        await expect(page.getByRole('button', { name: new RegExp(`^Project\\s+${escapeRegExp(e2eScenario.projectName)}`) })).toBeVisible();
        await expect(page.getByTitle('Refresh results').locator('svg')).not.toHaveClass(/animate-spin/);
    });

    await test.step('restore the scoped and unscoped states through Back and Forward', async () => {
        const projectFilter = page.getByRole('button', { name: new RegExp(`^Project\\s+${escapeRegExp(e2eScenario.projectName)}`) });
        await projectFilter.click();
        await expect(projectFilter).toHaveAttribute('aria-expanded', 'true');
        await page.getByRole('button', { name: 'Remove filter' }).click();
        await expect(page).not.toHaveURL(/[?&]project=/);

        await page.goBack();
        await expect(page).toHaveURL(new RegExp(`[?&]project=${e2eScenario.projectId}(?:&|$)`));
        await expect(projectFilter).toBeVisible();

        await page.goForward();
        await expect(page).not.toHaveURL(/[?&]project=/);
        await expect(projectFilter).toBeHidden();
    });
});
