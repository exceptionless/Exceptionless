import { expect, test } from '../fixtures/e2e-test';
import { createRepresentativeEvent } from '../support/synthetic-event';

const EVENT_COUNT = 5;
const PAGE_SIZE = 2;

test('deleting everything matching the filter runs in the background and shows progress', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange: more events than fit on one page, all in this test's own project.
    const references = Array.from({ length: EVENT_COUNT }, (_, index) => `${e2eScenario.referenceId}-bulk-${index}`);
    for (const referenceId of references) {
        await e2eApi.submitEvent(
            e2eScenario.projectId,
            e2eScenario.projectToken,
            createRepresentativeEvent({
                appUrl: e2eApi.environment.appUrl,
                message: `${e2eScenario.message} ${referenceId}`,
                referenceId,
                runId: e2eApi.environment.runId
            })
        );
    }

    for (const referenceId of references) {
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId);
    }

    await page.goto(`/event?project=${e2eScenario.projectId}&time=all&limit=${PAGE_SIZE}`);
    const table = page.locator('table:has(thead [role="checkbox"])').first();
    await expect(table.getByRole('checkbox', { name: 'Select row' })).toHaveCount(PAGE_SIZE, { timeout: 30_000 });

    await test.step('selecting the page offers to select everything matching the filter', async () => {
        // Act
        await table.getByRole('checkbox', { name: 'Select all rows' }).click();

        // Assert
        await expect(page.getByRole('status').filter({ hasText: `All ${PAGE_SIZE} events on this page are selected.` })).toBeVisible();
        await expect(page.getByRole('button', { name: `Select all ${EVENT_COUNT} events matching this filter` })).toBeVisible();
    });

    await test.step('selecting everything matching the filter shows the total', async () => {
        // Act
        await page.getByRole('button', { name: `Select all ${EVENT_COUNT} events matching this filter` }).click();

        // Assert
        await expect(page.getByRole('status').filter({ hasText: `All ${EVENT_COUNT} events are selected` })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Clear selection' })).toBeVisible();
    });

    await test.step('clearing the selection deselects the page', async () => {
        // Act
        await page.getByRole('button', { name: 'Clear selection' }).click();

        // Assert
        await expect(page.getByRole('button', { name: 'Clear selection' })).toBeHidden();
        await expect(table.getByRole('checkbox', { name: 'Select all rows' })).not.toBeChecked();
    });

    await test.step('deleting against the filter asks to confirm the exact count', async () => {
        // Arrange
        await table.getByRole('checkbox', { name: 'Select all rows' }).click();
        await page.getByRole('button', { name: `Select all ${EVENT_COUNT} events matching this filter` }).click();

        // Act
        await page.getByRole('button', { name: 'Actions' }).click();
        await page.getByRole('menuitem', { name: 'Delete' }).click();

        // Assert
        await expect(page.getByRole('alertdialog')).toContainText(`Delete ${EVENT_COUNT} events matching this filter? This can't be undone.`);
    });

    await test.step('confirming runs in the background and the progress panel reaches completion', async () => {
        // Act
        await page
            .getByRole('alertdialog')
            .getByRole('button', { name: `Delete ${EVENT_COUNT} Events` })
            .click();

        // Assert
        const panel = page.getByRole('region', { name: 'Background tasks' });
        await expect(panel).toContainText(/Delete \d+ events matching filter/, { timeout: 30_000 });
        await expect(panel).toContainText(`Completed · ${EVENT_COUNT} processed`, { timeout: 60_000 });
        await expect(table.getByRole('checkbox', { name: 'Select row' })).toHaveCount(0, { timeout: 30_000 });
    });

    await test.step('every event is gone', async () => {
        // Assert
        for (const referenceId of references) {
            await expect
                .poll(async () => (await e2eApi.getEventsByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId)).length, { timeout: 30_000 })
                .toBe(0);
        }
    });
});
