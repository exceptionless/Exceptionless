import type { Page } from '@playwright/test';

import type { E2EApiClient } from '../fixtures/api-client';

import { type E2EScenario, expect, test } from '../fixtures/e2e-test';

test.use({ e2eDismissGettingStartedChecklist: false, e2eUseGeneratedUser: true });

function checklist(page: Page) {
    return page.getByRole('region', { name: "Exie's checklist" });
}

async function expectChecklistStaysHidden(page: Page): Promise<void> {
    await page.reload();
    await expect(page.getByRole('table')).toBeVisible({ timeout: 30_000 });
    await expect(checklist(page)).toBeHidden();

    await page.goto('/event/all');
    await expect(page.getByRole('table')).toBeVisible({ timeout: 30_000 });
    await expect(checklist(page)).toBeHidden();
}

async function seedFirstEvent(e2eApi: E2EApiClient, scenario: E2EScenario): Promise<void> {
    await e2eApi.submitEvent(scenario.projectId, scenario.projectToken, {
        message: scenario.message,
        reference_id: scenario.referenceId,
        type: 'error'
    });
    await e2eApi.pollForEventByReference(scenario.userToken, scenario.projectId, scenario.referenceId);
}

test('the next checklist link opens the item it names @onboarding', async ({ e2eApi, e2eScenario, page }) => {
    await seedFirstEvent(e2eApi, e2eScenario);
    await page.goto(await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email'));
    await expect(checklist(page)).toContainText('4 of 8 done', { timeout: 30_000 });

    await checklist(page).getByRole('link', { name: 'Review your notifications' }).click();

    await expect(page).toHaveURL(/\/account\/notifications(?:[?#]|$)/);
});

test('finishing every item retires the checklist for good @onboarding', async ({ e2eApi, e2eScenario, page }) => {
    await test.step('arrange everything except the last item', async () => {
        await seedFirstEvent(e2eApi, e2eScenario);
        for (const item of ['checklist-notifications', 'checklist-ai-tools', 'checklist-invite-team']) {
            await e2eApi.recordProductTour(e2eScenario.userToken, item);
        }

        // Verifying the email lands back on the stack list with the checklist one item short.
        await page.goto(await e2eApi.pollForMailLink(e2eScenario.email, 'verify-email'));
        await expect(checklist(page)).toContainText('7 of 8 done', { timeout: 30_000 });
    });

    await test.step('open the last item', async () => {
        const docs = page.waitForEvent('popup');
        await checklist(page).getByRole('link', { name: 'Track versions & environments' }).click();
        await (await docs).close();

        await expect(page.getByText("You finished Exie's checklist!")).toBeVisible();
        await expect(checklist(page)).toBeHidden();
    });

    await test.step('the checklist stays hidden after reload and on other pages', async () => {
        await expectChecklistStaysHidden(page);
    });
});

test('dismissing the checklist hides it for good @onboarding', async ({ e2eApi, e2eScenario, page }) => {
    await seedFirstEvent(e2eApi, e2eScenario);
    await page.goto('/stack/all');
    await expect(checklist(page)).toContainText('3 of 8 done', { timeout: 30_000 });

    const recorded = page.waitForResponse((response) => response.url().includes('/product-tours/checklist-dismissed/record') && response.ok());
    await checklist(page).getByRole('button', { name: 'Dismiss checklist' }).click();
    await recorded;
    await expect(checklist(page)).toBeHidden();

    await expectChecklistStaysHidden(page);
});
