import type { CDPSession, Page } from '@playwright/test';

import { expect, test } from '../fixtures/organization-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { getWebSocketConnectionState, installWebSocketTestHarness } from '../support/web-socket';

for (const route of ['project/list', 'stream']) {
    test(`${route} keeps organization effects bounded through repeated switches and visibility changes`, async ({
        e2eApi,
        e2eScenario,
        impersonatedOrganization,
        page
    }) => {
        await Promise.all([
            seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eScenario),
            seedRepresentativeEvent(e2eApi, impersonatedOrganization.ownerToken, impersonatedOrganization)
        ]);
        await installWebSocketTestHarness(page);
        const runtimeErrors: string[] = [];
        page.on('pageerror', (error) => runtimeErrors.push(error.message));
        let listRequests = 0;
        page.on('request', (request) => {
            if (/\/api\/v2\/organizations\/[^/]+\/(?:projects|events)(?:\/count)?$/.test(new URL(request.url()).pathname)) {
                listRequests++;
            }
        });

        await page.goto(`/next/${route}`);
        await expect(page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first()).toBeVisible();
        await expect.poll(async () => (await getWebSocketConnectionState(page)).activeOrganizationIds).toEqual([e2eScenario.organizationId]);
        const session = await page.context().newCDPSession(page);
        const organizationTab = await page.context().newPage();
        try {
            await organizationTab.goto('/next/status');
            const listeners = await getListenerCounts(session);
            let connectionCount = (await getWebSocketConnectionState(page)).created;

            for (let index = 0; index < 6; index++) {
                const selected = index % 2 === 0 ? impersonatedOrganization : e2eScenario;
                const requestCount = listRequests;
                await organizationTab.evaluate(
                    (organizationId) => localStorage.setItem('organization', JSON.stringify(organizationId)),
                    selected.organizationId
                );
                await expect(page.getByRole('button').filter({ hasText: selected.organizationName }).filter({ visible: true }).first()).toBeVisible();
                await expect(
                    page
                        .getByText(route === 'stream' ? selected.message : selected.projectName)
                        .filter({ visible: true })
                        .first()
                ).toBeVisible();
                await expect
                    .poll(() => getWebSocketConnectionState(page))
                    .toEqual({
                        activeOrganizationIds: [selected.organizationId],
                        created: ++connectionCount,
                        pending: 0
                    });
                // Observe beyond a render flush to catch asynchronous request/reconnect loops too.
                await page.waitForTimeout(500);
                expect((await getWebSocketConnectionState(page)).created).toBe(connectionCount);
                expect(listRequests - requestCount).toBeLessThanOrEqual(4);
                expect(await getListenerCounts(session)).toEqual(listeners);
            }

            for (let index = 0; index < 5; index++) {
                await setDocumentHidden(page, true);
                await expect.poll(async () => (await getWebSocketConnectionState(page)).activeOrganizationIds).toEqual([]);
                await setDocumentHidden(page, false);
                await expect
                    .poll(() => getWebSocketConnectionState(page))
                    .toEqual({
                        activeOrganizationIds: [e2eScenario.organizationId],
                        created: ++connectionCount,
                        pending: 0
                    });
                expect(await getListenerCounts(session)).toEqual(listeners);
            }

            // Allow the final resume's intentional query invalidation to finish before checking idle stability.
            await page.waitForTimeout(1_000);
            const settledRequestCount = listRequests;
            await page.waitForTimeout(1_000);
            expect(listRequests).toBe(settledRequestCount);
            expect((await getWebSocketConnectionState(page)).created).toBe(connectionCount);
            expect(runtimeErrors).toEqual([]);
        } finally {
            await organizationTab.close();
            await session.detach();
        }
    });
}

async function getListenerCounts(session: CDPSession): Promise<{ keydown: number; visibilitychange: number }> {
    const response = await session.send('Runtime.evaluate', {
        expression:
            '({keydown: (getEventListeners(document).keydown ?? []).length, visibilitychange: (getEventListeners(document).visibilitychange ?? []).length})',
        includeCommandLineAPI: true,
        returnByValue: true
    });
    return response.result.value as { keydown: number; visibilitychange: number };
}

async function setDocumentHidden(page: Page, hidden: boolean): Promise<void> {
    await page.evaluate((nextHidden) => {
        Object.defineProperty(document, 'hidden', { configurable: true, get: () => nextHidden });
        Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => (nextHidden ? 'hidden' : 'visible') });
        document.dispatchEvent(new Event('visibilitychange'));
        window.dispatchEvent(new Event('visibilitychange'));
    }, hidden);
}
