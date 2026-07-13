import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleRow, getVisibleText } from '../support/page-helpers';

test('operator can navigate from event discovery to event and stack details through the UI', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);

    await test.step('seed a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('open event details from the Events table', async () => {
        await page.goto('/next/stack');
        await page.getByRole('link', { exact: true, name: 'Events' }).filter({ visible: true }).first().click();
        await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible();

        await page.goto(`/next/event?reference=${encodeURIComponent(journey.referenceId)}&time=all`);
        const eventRow = getVisibleRow(page, journey.message);
        await expect(eventRow).toBeVisible({ timeout: 30_000 });
        await eventRow.click();

        const eventSheet = page.getByRole('dialog', { name: 'Event' });
        await expect(eventSheet).toBeVisible();
        await expect(eventSheet.getByText(journey.message).filter({ visible: true }).first()).toBeVisible();
        await eventSheet.getByRole('link', { name: 'Open details in new window' }).click();

        await expect(page).toHaveURL(new RegExp(`/next/stack/${journey.stackId}/event/${journey.eventId}(?:[?#]|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });

    await test.step('move from the event to its stack occurrences', async () => {
        await page.getByRole('button', { name: 'Show all events' }).click();

        await expect(page).toHaveURL(new RegExp(`[?&]stack=${journey.stackId}(?:&|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });
    });

    await test.step('open stack details from the Stacks table', async () => {
        const stackViewLink = page.getByRole('link', { name: 'Most Frequent Errors' }).filter({ visible: true }).first();
        if (!(await stackViewLink.isVisible())) {
            await page.getByRole('button', { exact: true, name: 'Stacks' }).filter({ visible: true }).first().click();
        }

        await stackViewLink.click();
        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible();

        const stackRow = getVisibleRow(page, journey.message);
        await expect(stackRow).toBeVisible({ timeout: 30_000 });
        await stackRow.click();

        const stackSheet = page.getByRole('dialog', { name: 'Stack' });
        await expect(stackSheet).toBeVisible();
        await expect(stackSheet.getByText(journey.message).filter({ visible: true }).first()).toBeVisible();
        await stackSheet.getByRole('link', { name: 'Open details in new window' }).click();

        await expect(page).toHaveURL(new RegExp(`/next/stack/${journey.stackId}(?:/event/${journey.eventId})?(?:[?#]|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });
});
