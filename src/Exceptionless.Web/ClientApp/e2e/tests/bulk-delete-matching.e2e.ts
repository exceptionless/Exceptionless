import { BackgroundTaskStatus, type ViewBackgroundTask } from '#generated/api.ts';

import { expect, test } from '../fixtures/e2e-test';
import { createRepresentativeEvent } from '../support/synthetic-event';

const EVENT_COUNT = 5;
const PAGE_SIZE = 2;

test('many running tasks stay within the viewport and the oldest task can be cancelled after scrolling', async ({ e2eScenario, page }) => {
    // Arrange
    const now = Date.now();
    const tasks: ViewBackgroundTask[] = Array.from({ length: 12 }, (_, index) => ({
        can_cancel: true,
        created_by_user_id: '000000000000000000000001',
        created_utc: new Date(now - index * 1000).toISOString(),
        description: `Synthetic bulk task ${index + 1}`,
        failed: 0,
        id: (index + 1).toString(16).padStart(24, '0'),
        organization_id: e2eScenario.organizationId,
        processed: 25,
        progress: 25,
        project_id: e2eScenario.projectId,
        status: BackgroundTaskStatus.Running,
        succeeded: 25,
        total: 100,
        type: 'events.delete',
        updated_utc: new Date(now).toISOString()
    }));
    const oldest = tasks.at(-1)!;
    await page.route(
        (url) => url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}/tasks`,
        (route) => route.fulfill({ json: tasks })
    );
    await page.route(`**/api/v2/tasks/${oldest.id}/cancel`, async (route) => {
        oldest.status = BackgroundTaskStatus.Cancelled;
        oldest.updated_utc = new Date().toISOString();
        await route.fulfill({ json: oldest, status: 202 });
    });
    await page.setViewportSize({ height: 400, width: 390 });
    const panel = page.getByRole('region', { name: 'Background tasks' });
    const oldestCancel = panel.getByRole('button', { exact: true, name: `Cancel ${oldest.description}` });

    // Act
    await page.goto(`/event?project=${e2eScenario.projectId}&time=all`);

    // Assert
    await expect(panel.getByRole('button', { name: /^Cancel Synthetic bulk task / })).toHaveCount(tasks.length);
    const metrics = await panel.evaluate((element) => {
        const bounds = element.getBoundingClientRect();
        return {
            bottom: bounds.bottom,
            clientHeight: element.clientHeight,
            scrollHeight: element.scrollHeight,
            top: bounds.top,
            viewportHeight: window.innerHeight
        };
    });
    expect(metrics.top).toBeGreaterThanOrEqual(0);
    expect(metrics.bottom).toBeLessThanOrEqual(metrics.viewportHeight);
    expect(metrics.scrollHeight).toBeGreaterThan(metrics.clientHeight);
    await expect(oldestCancel).not.toBeInViewport();

    // Act
    await oldestCancel.scrollIntoViewIfNeeded();

    // Assert
    await expect(oldestCancel).toBeInViewport();
    expect(await panel.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);

    // Act
    await oldestCancel.click();

    // Assert
    await expect(panel.getByRole('button', { exact: true, name: `Dismiss ${oldest.description}` })).toBeVisible();
});

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
