import type { Page } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

test('events filter builder applies, persists, and clears a reference filter', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const missingReference = `${journey.referenceId}-missing`;

    await test.step('submit a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('apply a reference filter through the filter builder', async () => {
        await page.goto('/next/event?time=all');
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

        await setReferenceFilter(page, missingReference);
        await expect(page).toHaveURL(new RegExp(`[?&]reference=${escapeRegExp(missingReference)}(?:&|$)`));
        await expect(getVisibleText(page, 'No data was found with the current filter.')).toBeVisible({ timeout: 30_000 });

        await setReferenceFilter(page, journey.referenceId);
        await expect(page).toHaveURL(new RegExp(`[?&]reference=${escapeRegExp(journey.referenceId)}(?:&|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });
    });

    await test.step('persist the filter through reload and then clear it', async () => {
        await page.reload();
        const referenceFilter = page
            .getByRole('button', { name: new RegExp(`^Reference\\s+${escapeRegExp(journey.referenceId)}`) })
            .filter({ visible: true })
            .first();
        await expect(referenceFilter).toBeVisible();
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

        await referenceFilter.click();
        await page.getByRole('button', { name: 'Remove filter' }).click();

        await expect(page).not.toHaveURL(/[?&]reference=/);
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });
    });
});

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function setReferenceFilter(page: Page, referenceId: string): Promise<void> {
    const existingReferenceFilter = page
        .getByRole('button', { name: /^Reference/ })
        .filter({ visible: true })
        .first();

    if (await existingReferenceFilter.isVisible()) {
        await existingReferenceFilter.click();
    } else {
        await page.getByRole('button', { name: 'Manage filters' }).click();
        await page.getByPlaceholder('Search...').fill('Reference');
        await page.getByText('Reference', { exact: true }).click();
    }

    const input = page.getByLabel('Filter by Reference');
    await input.fill(referenceId);
    await input.press('Enter');
}
