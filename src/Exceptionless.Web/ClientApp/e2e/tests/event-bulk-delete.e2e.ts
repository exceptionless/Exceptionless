import type { Page } from '@playwright/test';

import { ChangeType, type WebSocketMessage } from '../../src/lib/features/websockets/models';
import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { installWebSocketTestHarness, setDocumentHidden, waitForWebSocketConnection } from '../support/web-socket';

test('bulk event deletion reconciles rows and totals with live and missing removal messages', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    const messages: string[] = [];
    for (let index = 0; index < 4; index++) {
        const message = `Bulk delete ${index} ${e2eScenario.run}`;
        messages.push(message);
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: `${e2eScenario.referenceId}-${index}`
        });
    }

    let dropServerMessages = false;
    let removalMessages = 0;
    let deleteRequests = 0;
    page.on('request', (request) => {
        if (request.method() === 'DELETE' && new URL(request.url()).pathname.startsWith('/api/v2/events/')) {
            deleteRequests++;
        }
    });
    await page.routeWebSocket(
        (url) => url.pathname === '/api/v2/push',
        (socket) => {
            const server = socket.connectToServer();
            server.onMessage((message) => {
                if (typeof message === 'string') {
                    const notification = JSON.parse(message) as WebSocketMessage<'PersistentEventChanged'>;
                    if (notification.type === 'PersistentEventChanged' && notification.message.change_type === ChangeType.Removed) {
                        removalMessages++;
                    }
                }
                if (!dropServerMessages) {
                    socket.send(message);
                }
            });
        }
    );

    await page.goto(`/event?project=${e2eScenario.projectId}&time=all`);
    for (const message of messages) {
        await expect(eventRow(page, message)).toBeVisible();
    }
    const eventTotal = page.locator('[data-slot="card"]').filter({ has: page.getByRole('button', { exact: true, name: 'About events' }) });
    await expect(eventTotal.locator('[data-slot="card-content"]')).toHaveText('4');

    let holdListRefetch = false;
    let releaseRefetch: () => void = () => {};
    const refetchReleased = new Promise<void>((resolve) => (releaseRefetch = resolve));
    await page.route(`**/api/v2/organizations/${e2eScenario.organizationId}/events?*`, async (route) => {
        if (holdListRefetch) {
            await refetchReleased;
        }
        await route.continue();
    });

    await test.step('cancelling confirmation sends no delete and preserves selection', async () => {
        for (const message of messages.slice(0, 2)) {
            await eventRow(page, message).getByRole('checkbox', { name: 'Select row' }).check();
        }
        await openDeleteConfirmation(page, 2);
        await page.getByRole('button', { exact: true, name: 'Cancel' }).click();
        expect(deleteRequests).toBe(0);
        await expect(page.getByText('2 selected', { exact: true })).toBeVisible();
    });

    await test.step('two selected rows disappear and totals reconcile without a manual refresh', async () => {
        await page.screenshot({ path: testInfo.outputPath('before-live-delete.png') });
        holdListRefetch = true;
        try {
            await openDeleteConfirmation(page, 2);
            await confirmDelete(page, 2);
            await expect(page.getByText('Successfully deleted 2 events.', { exact: true })).toBeVisible();
            await expect.poll(() => removalMessages).toBeGreaterThanOrEqual(2);
            for (const message of messages.slice(0, 2)) {
                await expect(eventRow(page, message)).toHaveCount(0);
            }
            for (const message of messages.slice(2)) {
                await expect(eventRow(page, message)).toBeVisible();
            }
            await expect(page.getByText('2 selected', { exact: true })).toHaveCount(0);

            // Hold the real refetch so it cannot hide a stale-row restoration.
            await eventRow(page, messages[2]!).getByRole('checkbox', { name: 'Select row' }).check();
            for (const message of messages.slice(0, 2)) {
                await expect(eventRow(page, message)).toHaveCount(0);
            }
            await page.screenshot({ path: testInfo.outputPath('before-authoritative-refetch.png') });

            const eventsUrl = page.url();
            const stacksLink = page.locator('a[href="/stack/all"]');
            if (!(await stacksLink.isVisible())) {
                await page.getByRole('button', { exact: true, name: 'Stacks' }).click();
            }
            await stacksLink.click();
            await expect(page).toHaveURL(/\/stack\/all/);
            await expect(page.getByRole('heading', { exact: true, name: 'All' })).toBeVisible();
            await page.goBack();
            await expect(page).toHaveURL(eventsUrl);
            await expect(eventRow(page, messages[2]!)).toBeVisible();
            for (const message of messages.slice(0, 2)) {
                await expect(eventRow(page, message)).toHaveCount(0);
            }
            await eventRow(page, messages[2]!).getByRole('checkbox', { name: 'Select row' }).check();
        } finally {
            holdListRefetch = false;
            releaseRefetch();
        }
        await expect(eventTotal.locator('[data-slot="card-content"]')).toHaveText('2');
        await page.screenshot({ path: testInfo.outputPath('after-live-delete.png') });
    });

    await test.step('mutation reconciliation recovers when server messages are dropped', async () => {
        dropServerMessages = true;
        await eventRow(page, messages[3]!).getByRole('checkbox', { name: 'Select row' }).check();
        await openDeleteConfirmation(page, 2);
        await page.route('**/api/v2/events/*', async (route) => {
            if (route.request().method() === 'DELETE') {
                await route.fulfill({
                    body: JSON.stringify({ status: 400, title: 'Deletion was rejected' }),
                    contentType: 'application/problem+json',
                    status: 400
                });
            } else {
                await route.fallback();
            }
        });
        const rejectedDelete = page.waitForResponse((response) => response.request().method() === 'DELETE');
        await page.getByRole('button', { exact: true, name: 'Delete 2 Events' }).click();
        expect((await rejectedDelete).status()).toBe(400);
        await expect(page.getByText('Deletion was rejected', { exact: true })).toBeVisible();
        await expect(page.getByRole('alertdialog')).toBeVisible();
        await expect(page.getByText('2 selected', { exact: true })).toBeVisible();
        for (const message of messages.slice(2)) {
            await expect(eventRow(page, message)).toBeVisible();
        }
        await page.unroute('**/api/v2/events/*');
        await confirmDelete(page, 2);
        await expect(page.getByText('No data was found with the current filter.')).toBeVisible();
        await expect(eventTotal.locator('[data-slot="card-content"]')).toHaveText('0');
        await page.screenshot({ path: testInfo.outputPath('after-delete-without-messages.png') });

        await page.reload();
        await expect(page.getByText('No data was found with the current filter.')).toBeVisible();
        await expect(eventTotal.locator('[data-slot="card-content"]')).toHaveText('0');
        expect(deleteRequests).toBe(3);
    });
});

test('deleting the last event on a cursor page returns to populated results', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    const messages: string[] = [];
    for (let index = 0; index < 3; index++) {
        const message = `Cursor delete ${index} ${e2eScenario.run}`;
        messages.push(message);
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: `${e2eScenario.referenceId}-${index}`
        });
    }
    await page.goto(`/event?project=${e2eScenario.projectId}&time=all&limit=2`);
    const pager = page.getByRole('navigation', { name: 'Table pagination' });
    await expect(pager.getByLabel('Page 1 of 2')).toBeVisible();
    await pager.getByRole('button', { name: 'Go to next page' }).click();
    await expect(pager.getByLabel('Page 2 of 2')).toBeVisible();
    await expect(page.getByRole('checkbox', { exact: true, name: 'Select row' })).toHaveCount(1);
    const row = page.getByRole('row').filter({ has: page.getByRole('checkbox', { exact: true, name: 'Select row' }) });
    const deletedSummary = await row.innerText();
    const deletedMessage = messages.find((message) => deletedSummary.includes(message));
    expect(deletedMessage).toBeTruthy();
    await row.getByRole('checkbox', { name: 'Select row' }).check();
    await openDeleteConfirmation(page, 1);
    await confirmDelete(page, 1);

    await expect(pager.getByLabel('Page 1 of 1')).toBeVisible();
    await expect(page.getByRole('checkbox', { exact: true, name: 'Select row' })).toHaveCount(2);
    await expect(page.getByText('No data was found with the current filter.')).toBeHidden();
    await expect(eventRow(page, deletedMessage!)).toHaveCount(0);
    await page.screenshot({ path: testInfo.outputPath('after-last-page-delete.png') });
});

test('a paused event stream does not restore deleted rows when the next batch arrives', async ({ context, e2eApi, e2eScenario, page }, testInfo) => {
    const messages = [`Stream deleted ${e2eScenario.run}`, `Stream retained ${e2eScenario.run}`, `Stream added ${e2eScenario.run}`];
    const seed = async (index: number) =>
        seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: messages[index]!,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: `${e2eScenario.referenceId}-${index}`
        });
    await seed(0);
    await seed(1);
    await installWebSocketTestHarness(page);
    await page.goto(`/stream?project=${e2eScenario.projectId}&time=all`);
    await expect(eventRow(page, messages[0]!)).toBeVisible();
    await expect(eventRow(page, messages[1]!)).toBeVisible();
    await page.getByTitle('Pause streaming updates').click();
    await expect(page.getByTitle('Resume streaming updates')).toBeVisible();

    const deletePage = await context.newPage();
    try {
        await deletePage.goto(`/event?project=${e2eScenario.projectId}&time=all`);
        await eventRow(deletePage, messages[0]!).getByRole('checkbox', { name: 'Select row' }).check();
        await openDeleteConfirmation(deletePage, 1);
        await confirmDelete(deletePage, 1);
        await expect(eventRow(page, messages[0]!)).toHaveCount(0);

        await seed(2);
        await page.getByTitle('Resume streaming updates').click();
        await expect(eventRow(page, messages[2]!)).toBeVisible();
        await expect(eventRow(page, messages[1]!)).toBeVisible();
        await expect(eventRow(page, messages[0]!)).toHaveCount(0);
        await page.screenshot({ path: testInfo.outputPath('stream-after-delete-and-resume.png') });

        await test.step('a paused stream reconciles missed deletions when resumed after reconnect recovery', async () => {
            await page.getByTitle('Pause streaming updates').click();
            await setDocumentHidden(page, true);
            await waitForWebSocketDisconnection(page);
            await eventRow(deletePage, messages[1]!).getByRole('checkbox', { name: 'Select row' }).check();
            await openDeleteConfirmation(deletePage, 1);
            await confirmDelete(deletePage, 1);
            await expect(eventRow(page, messages[1]!)).toBeVisible();

            await page.evaluate(() => {
                const tracked = window as Window & { __exceptionlessE2ERefreshes?: number };
                tracked.__exceptionlessE2ERefreshes = 0;
                document.addEventListener('refresh', () => tracked.__exceptionlessE2ERefreshes!++);
            });
            await setDocumentHidden(page, false);
            await waitForWebSocketConnection(page);
            await page.waitForFunction(() => (window as Window & { __exceptionlessE2ERefreshes?: number }).__exceptionlessE2ERefreshes === 2);
            await expect(page.getByTitle('Resume streaming updates')).toBeVisible();
            await page.getByTitle('Resume streaming updates').click();
            await expect(eventRow(page, messages[1]!)).toHaveCount(0);
            await expect(eventRow(page, messages[2]!)).toBeVisible();
            await page.screenshot({ path: testInfo.outputPath('stream-after-missed-delete-and-resume.png') });
        });
    } finally {
        await deletePage.close();
    }
});

test('late responses and a disconnected browser do not retain deleted events', async ({ context, e2eApi, e2eScenario, page }, testInfo) => {
    const messages: string[] = [];
    for (let index = 0; index < 3; index++) {
        const message = `Deletion recovery ${index} ${e2eScenario.run}`;
        messages.push(message);
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: `${e2eScenario.referenceId}-${index}`
        });
    }
    await installWebSocketTestHarness(page);
    await page.goto(`/event?project=${e2eScenario.projectId}&time=all`);
    await expect(eventRow(page, messages[0]!)).toBeVisible();
    await waitForWebSocketConnection(page);
    const eventTotal = page.locator('[data-slot="card"]').filter({ has: page.getByRole('button', { exact: true, name: 'About events' }) });
    await expect(eventTotal.locator('[data-slot="card-content"]')).toHaveText('3');

    let captureNextRead = true;
    let readCaptured: () => void = () => {};
    const captured = new Promise<void>((resolve) => (readCaptured = resolve));
    let releaseOldRead: () => void = () => {};
    const oldReadReleased = new Promise<void>((resolve) => (releaseOldRead = resolve));
    let holdFreshReads = true;
    let releaseFreshReads: () => void = () => {};
    const freshReadsReleased = new Promise<void>((resolve) => (releaseFreshReads = resolve));
    await page.route(`**/api/v2/organizations/${e2eScenario.organizationId}/events?*`, async (route) => {
        if (captureNextRead) {
            captureNextRead = false;
            const response = await route.fetch();
            readCaptured();
            await oldReadReleased;
            await route.fulfill({ response });
        } else {
            if (holdFreshReads) {
                await freshReadsReleased;
            }
            await route.continue();
        }
    });
    const deletePage = await context.newPage();
    try {
        await deletePage.goto(`/event?project=${e2eScenario.projectId}&time=all`);
        await expect(eventRow(deletePage, messages[0]!)).toBeVisible();
        await test.step('deliver a real pre-deletion response after a confirmed deletion', async () => {
            await page.getByTitle('Refresh results').click();
            await captured;
            await eventRow(deletePage, messages[0]!).getByRole('checkbox', { name: 'Select row' }).check();
            await openDeleteConfirmation(deletePage, 1);
            await confirmDelete(deletePage, 1);
            await expect(eventRow(page, messages[0]!)).toHaveCount(0);
            releaseOldRead();
            await expect(eventTotal.locator('[data-slot="card-content"]')).toHaveText('2');
            await expect(eventRow(page, messages[0]!)).toHaveCount(0);
            await expect(eventRow(page, messages[1]!)).toBeVisible();
            await page.screenshot({ path: testInfo.outputPath('after-late-response.png') });
        });

        holdFreshReads = false;
        releaseFreshReads();
        await page.unrouteAll({ behavior: 'wait' });
        await test.step('reconcile a deletion missed while disconnected', async () => {
            await setDocumentHidden(page, true);
            await waitForWebSocketDisconnection(page);
            await eventRow(deletePage, messages[1]!).getByRole('checkbox', { name: 'Select row' }).check();
            await openDeleteConfirmation(deletePage, 1);
            await confirmDelete(deletePage, 1);
            await expect(eventRow(page, messages[1]!)).toBeVisible();
            await setDocumentHidden(page, false);
            await waitForWebSocketConnection(page);
            await expect(eventRow(page, messages[1]!)).toHaveCount(0);
            await expect(eventRow(page, messages[2]!)).toBeVisible();
            await expect(eventTotal.locator('[data-slot="card-content"]')).toHaveText('1');
            await page.screenshot({ path: testInfo.outputPath('after-reconnect.png') });
        });
    } finally {
        releaseOldRead();
        releaseFreshReads();
        await deletePage.close();
    }
});

async function confirmDelete(page: Page, count: number) {
    const deletion = page.waitForResponse(
        (response) => response.request().method() === 'DELETE' && new URL(response.url()).pathname.startsWith('/api/v2/events/')
    );
    await page.getByRole('button', { exact: true, name: count === 1 ? 'Delete Event' : `Delete ${count} Events` }).click();
    expect((await deletion).status()).toBe(202);
    await expect(page.getByRole('alertdialog')).toHaveCount(0);
}

function eventRow(page: Page, message: string) {
    return page.getByRole('row').filter({ hasText: message });
}

async function openDeleteConfirmation(page: Page, count: number) {
    await page.getByRole('button', { exact: true, name: 'Actions' }).click();
    await page.getByRole('menuitem', { exact: true, name: 'Delete' }).click();
    await expect(page.getByRole('alertdialog')).toBeVisible();
    await expect(page.getByRole('heading', { exact: true, name: count === 1 ? 'Delete Event' : `Delete ${count} Events` })).toBeVisible();
}

async function waitForWebSocketDisconnection(page: Page) {
    await page.waitForFunction(() => {
        const tracked = window as Window & { __exceptionlessE2EWebSockets?: WebSocket[] };
        return tracked.__exceptionlessE2EWebSockets
            ?.filter((socket) => new URL(socket.url).pathname === '/api/v2/push')
            .every((socket) => socket.readyState === WebSocket.CLOSED);
    });
}
