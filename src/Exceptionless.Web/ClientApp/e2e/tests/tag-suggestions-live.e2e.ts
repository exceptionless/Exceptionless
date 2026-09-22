import { expect, test } from '../fixtures/e2e-test';
import { getVisibleText } from '../support/page-helpers';
import { createRepresentativeEvent } from '../support/synthetic-event';

test('tag suggestions from indexed events can be selected and survive reload', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, {
        ...createRepresentativeEvent({
            appUrl: e2eApi.environment.appUrl,
            message: e2eScenario.message,
            referenceId: e2eScenario.referenceId,
            runId: e2eScenario.run
        }),
        tags: ['AlphaTag', 'BetaTag']
    });
    await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, e2eScenario.referenceId);

    await page.goto('/next/event?tag=AlphaTag&time=all');
    await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
    await page.getByRole('button', { name: /^Tag\s+AlphaTag/ }).click();
    await expect(page.getByRole('option', { exact: true, name: 'BetaTag' })).toBeVisible();
    await page.getByPlaceholder('Tag', { exact: true }).fill('Beta');
    await page.getByRole('option', { exact: true, name: 'BetaTag' }).click();
    await expect(page).toHaveURL(/BetaTag/);
    await expect(page).toHaveURL(/AlphaTag/);
    await page.getByPlaceholder('Tag', { exact: true }).press('Escape');
    await page.reload();

    await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
    await page.getByRole('button', { name: /^Tag\s+AlphaTag\s+BetaTag/ }).click();
    await expect(page.getByRole('option', { exact: true, name: 'AlphaTag' })).toBeVisible();
    await expect(page.getByRole('option', { exact: true, name: 'BetaTag' })).toBeVisible();
    await expect(page.getByPlaceholder('Tag', { exact: true })).toHaveValue('');
    await page.screenshot({ path: testInfo.outputPath('tag-suggestions.png') });
});
