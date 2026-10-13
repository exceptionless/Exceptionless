import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';
import { installWebSocketTestHarness, waitForWebSocketConnection } from '../support/web-socket';

test('new user can send an event and find it in primary views @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await installWebSocketTestHarness(page);

    await test.step('receive an ingested event through the live server connection', async () => {
        const initialList = page.waitForResponse((response) => {
            const url = new URL(response.url());
            return url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}/events` && url.searchParams.get('mode') === 'summary';
        });
        await page.goto('/event');
        const response = await initialList;
        expect(response.ok()).toBe(true);
        expect(await response.json()).toEqual([]);
        await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible();
        await waitForWebSocketConnection(page);
        await expect(page.getByTitle('Refresh results').locator('svg')).not.toHaveClass(/animate-spin/);
        await journey.submitRepresentativeEvent();
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });
    });

    await test.step('find the event in Events, Stacks, and Event Stream', async () => {
        await journey.expectEventInPrimaryViews();
    });
});
