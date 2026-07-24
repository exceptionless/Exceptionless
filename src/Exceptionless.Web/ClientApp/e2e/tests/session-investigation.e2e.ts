import { expect, test } from '../fixtures/e2e-test';
import { getVisibleRow, getVisibleText } from '../support/page-helpers';
import { createSessionEvent } from '../support/synthetic-event';

test('operator can find and inspect a user session', async ({ e2eApi, e2eScenario, page }) => {
    const sessionId = `${e2eScenario.referenceId}-session`;
    const identity = `session-${e2eScenario.run}@exceptionless.test`;
    const name = `Session User ${e2eScenario.run}`;

    await test.step('seed a representative session', async () => {
        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, createSessionEvent({ identity, name, sessionId }));
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, sessionId);
    });

    await test.step('open the session from the Sessions table', async () => {
        await page.goto('/next/sessions?time=all');
        await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible();

        const sessionRow = getVisibleRow(page, name, identity);
        await expect(sessionRow).toBeVisible({ timeout: 30_000 });
        await sessionRow.click();

        const eventSheet = page.getByRole('dialog', { name: 'Event' });
        await expect(eventSheet).toBeVisible();
        await expect(eventSheet.getByText(name).filter({ visible: true }).first()).toBeVisible();
        await eventSheet.getByRole('link', { name: 'Open details in new window' }).click();

        await expect(page).toHaveURL(/\/next\/(?:event|stack\/[^/]+\/event)\//);
        await expect(getVisibleText(page, identity)).toBeVisible();
    });
});
