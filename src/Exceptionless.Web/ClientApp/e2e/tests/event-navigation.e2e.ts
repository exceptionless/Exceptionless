import type { Route } from '@playwright/test';

import { createReferenceId, expect, test } from '../fixtures/e2e-test';
import { createRepresentativeEvent } from '../support/synthetic-event';

test('event navigation waits for the selected event before reusing neighbour links @signup', async ({ e2eApi, e2eScenario, page }) => {
    const now = Date.now();
    const seeds = Array.from({ length: 3 }, (_, index) => {
        const referenceId = createReferenceId(e2eScenario.run, `-navigation-${index}`);
        return {
            event: {
                ...createRepresentativeEvent({
                    appUrl: e2eApi.environment.appUrl,
                    message: e2eScenario.message,
                    referenceId,
                    runId: e2eScenario.run
                }),
                date: new Date(now - (3 - index) * 60_000).toISOString()
            },
            referenceId
        };
    });
    await Promise.all(seeds.map(({ event }) => e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, event)));
    const [olderEvent, middleEvent, newerEvent] = await Promise.all(
        seeds.map(({ referenceId }) => e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId))
    );
    expect(olderEvent.stack_id).toBeTruthy();
    expect(new Set([middleEvent.stack_id, newerEvent.stack_id, olderEvent.stack_id]).size).toBe(1);

    const middlePath = `/stack/${middleEvent.stack_id}/event/${middleEvent.id}`;
    const olderPath = `/stack/${olderEvent.stack_id}/event/${olderEvent.id}`;
    await page.goto(middlePath);
    const olderButton = page.getByRole('button', { name: 'Older event' });
    const newerButton = page.getByRole('button', { name: 'Newer event' });
    await expect(olderButton).toBeEnabled();
    await expect(newerButton).toBeEnabled();

    let blocked = false;
    let releaseRequest!: () => void;
    const pendingTransport = new Promise<void>((resolve) => {
        releaseRequest = resolve;
    });
    const matchOlderRequest = (url: URL) => url.pathname === `/api/v2/events/${olderEvent.id}`;
    const holdOlderRequest = async (route: Route) => {
        if (route.request().method() === 'GET') {
            blocked = true;
            await pendingTransport;
        }
        await route.continue();
    };
    await page.route(matchOlderRequest, holdOlderRequest);

    try {
        await olderButton.click();
        await expect(page).toHaveURL(new URL(olderPath, e2eApi.environment.appUrl).href);
        await expect.poll(() => blocked).toBe(true);
        await expect(olderButton).toBeDisabled();
        await expect(newerButton).toBeDisabled();

        releaseRequest();
        await expect(newerButton).toBeEnabled();
        await newerButton.click();
        await expect(page).toHaveURL(new URL(middlePath, e2eApi.environment.appUrl).href);
        await expect(olderButton).toBeEnabled();
        await expect(newerButton).toBeEnabled();
    } finally {
        releaseRequest();
        await page.unroute(matchOlderRequest, holdOlderRequest);
    }
});
