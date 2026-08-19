import type { ConsoleMessage, Page, Request, Response } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { createRepresentativeEvent } from '../support/synthetic-event';
import { dispatchWebSocketMessages, installWebSocketTestHarness } from '../support/web-socket';

const EVENT_NOTIFICATION_TRAILING_REFRESH_MS = 5_000;

interface ActionSample extends RequestCounts {
    name: string;
    requestFailures: number;
    runtimeErrors: number;
}

interface RequestCounts {
    eventCount: number;
    eventDetails: number;
    eventList: number;
    savedViews: number;
    stackDetails: number;
    stackEvents: number;
}

interface RuntimeDiagnostics {
    actionSamples: ActionSample[];
    activeAction: string;
    networkFailures: { action: string; status: number; url: string }[];
    requestFailures: { action: string; error: null | string; method: string; url: string }[];
    requests: RequestCounts;
    runtimeErrors: { action: string; message: string }[];
}

test('event list and detail effects stay bounded through paging and background chaos @signup', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    test.slow();
    await installWebSocketTestHarness(page);

    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const diagnostics: RuntimeDiagnostics = {
        actionSamples: [],
        activeAction: 'setup',
        networkFailures: [],
        requestFailures: [],
        requests: emptyRequestCounts(),
        runtimeErrors: []
    };

    page.on('console', (message) => recordConsoleMessage(diagnostics, message));
    page.on('pageerror', (error) => diagnostics.runtimeErrors.push({ action: diagnostics.activeAction, message: error.stack ?? error.message }));
    page.on('request', (request) => recordRequest(diagnostics, request, e2eScenario.organizationId));
    page.on('requestfailed', (request) => recordRequestFailure(diagnostics, request));
    page.on('response', (response) => recordNetworkFailure(diagnostics, response));

    await test.step('seed enough events in one stack to exercise list and detail navigation', async () => {
        await journey.submitRepresentativeEvent();
        const events = Array.from({ length: 8 }, (_, index) => createGroupedEvent(e2eApi.environment.appUrl, journey.message, e2eScenario.run, index));
        await Promise.all(events.map(({ event }) => e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, event)));
        const indexedEvents = await Promise.all(
            events.map(({ referenceId }) => e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId))
        );
        expect(new Set(indexedEvents.map((event) => event.stack_id))).toEqual(new Set([journey.stackId]));
    });

    await test.step('load the Events list with one page of results', async () => {
        const response = page.waitForResponse((candidate) => isEventListResponse(candidate, e2eScenario.organizationId));
        await page.goto('/next/event?filter=type%3Aerror&limit=5');
        const listResponse = await response;
        expect(listResponse.ok()).toBe(true);
        const events = (await listResponse.json()) as { id?: string }[];
        expect(events).toHaveLength(5);
        const middleEventId = events[2]?.id;
        expect(middleEventId).toBeTruthy();
        journey.eventId = middleEventId!;
        await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible();
        await expect(page.locator('tbody tr:visible').first()).toBeVisible();
        await expect(page.getByRole('button', { name: 'Go to next page' })).toBeEnabled();
    });

    await waitForEventListQuiescence(page, diagnostics);

    await measureAction(diagnostics, 'event list selected refresh', async () => {
        const rowSelection = page.getByRole('checkbox', { name: 'Select row' }).first();
        await rowSelection.click();
        await expect(rowSelection).toBeChecked();

        const response = page.waitForResponse((candidate) => isEventListResponse(candidate, e2eScenario.organizationId));
        await page.getByTitle('Return to the first page to refresh results').click();
        expect((await response).ok()).toBe(true);
        await expect(rowSelection).not.toBeChecked();
    });
    expect(actionSample(diagnostics, 'event list selected refresh').eventList).toBe(1);

    await measureAction(diagnostics, 'event list paging', async () => {
        for (let index = 0; index < 4; index++) {
            await clickAndWaitForPage(page, e2eScenario.organizationId, 'Go to next page', 2, index === 0);
            await expect(page.getByRole('button', { name: 'Go to previous page' })).toBeEnabled();
            await clickAndWaitForPage(page, e2eScenario.organizationId, 'Go to previous page', 1);
        }
    });
    expect(actionSample(diagnostics, 'event list paging').eventList).toBe(1);

    await measureAction(diagnostics, 'event detail sheet mount and teardown', async () => {
        for (let index = 0; index < 5; index++) {
            await page.locator('tbody tr:visible').first().click();
            await expect(page.getByRole('dialog')).toBeVisible();
            await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
            await page.getByRole('button', { name: 'Close' }).click();
            await expect(page.getByRole('dialog')).not.toBeVisible();
        }
    });
    expect(actionSample(diagnostics, 'event detail sheet mount and teardown')).toMatchObject({
        eventDetails: expect.any(Number),
        eventList: 0,
        stackEvents: 0
    });
    expect(actionSample(diagnostics, 'event detail sheet mount and teardown').eventDetails).toBeLessThanOrEqual(2);
    expect(actionSample(diagnostics, 'event detail sheet mount and teardown').stackDetails).toBeLessThanOrEqual(3);

    await measureAction(diagnostics, 'event detail sheet navigation', async () => {
        await page.locator('tbody tr:visible').first().click();
        await expect(page.getByRole('dialog')).toBeVisible();
        const olderEvent = page.getByRole('button', { name: 'Older event' });
        const newerEvent = page.getByRole('button', { name: 'Newer event' });

        for (let index = 0; index < 5; index++) {
            await expect(olderEvent).toBeEnabled();
            await olderEvent.click();
            await expect(newerEvent).toBeEnabled();
            await newerEvent.click();
        }

        await page.getByRole('button', { name: 'Close' }).click();
    });
    expect(actionSample(diagnostics, 'event detail sheet navigation').eventDetails).toBeLessThanOrEqual(2);
    expect(actionSample(diagnostics, 'event detail sheet navigation').eventList).toBe(0);

    await measureAction(diagnostics, 'event list visibility churn', async () => {
        for (let index = 0; index < 30; index++) {
            await setDocumentHidden(page, true);
            await setDocumentHidden(page, false);
        }

        await page.waitForTimeout(2_000);
    });
    expect(actionSample(diagnostics, 'event list visibility churn').eventList).toBeLessThanOrEqual(1);

    await measureAction(diagnostics, 'event list background ingestion and resume', async () => {
        await setDocumentHidden(page, true);
        const { event, referenceId } = createGroupedEvent(e2eApi.environment.appUrl, journey.message, e2eScenario.run, 100);
        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, event);
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId);
        await setDocumentHidden(page, false);
        await page.waitForTimeout(2_000);
    });
    expect(actionSample(diagnostics, 'event list background ingestion and resume').eventList).toBeLessThanOrEqual(2);

    await measureAction(diagnostics, 'sustained persistent event notifications', async () => {
        for (let wave = 0; wave < 4; wave++) {
            await dispatchWebSocketMessages(
                page,
                Array.from({ length: 30 }, (_, index) => ({
                    message: {
                        change_type: 0,
                        id: `chaos-missing-event-${wave}-${index}`,
                        organization_id: e2eScenario.organizationId,
                        project_id: e2eScenario.projectId,
                        stack_id: journey.stackId!,
                        type: 'PersistentEvent'
                    },
                    type: 'PersistentEventChanged'
                }))
            );
            await page.waitForTimeout(1_600);
        }

        await page.waitForTimeout(2_000);
    });
    const sustainedNotificationSample = actionSample(diagnostics, 'sustained persistent event notifications');
    expect(sustainedNotificationSample.eventCount).toBeGreaterThanOrEqual(1);
    expect(sustainedNotificationSample.eventCount).toBeLessThanOrEqual(3);
    expect(sustainedNotificationSample.eventList).toBeGreaterThanOrEqual(1);
    expect(sustainedNotificationSample.eventList).toBeLessThanOrEqual(3);
    expect(sustainedNotificationSample.runtimeErrors).toBe(0);

    await measureAction(diagnostics, 'event detail alias route remounts', async () => {
        for (let index = 0; index < 5; index++) {
            await page.goto(`/next/event/${journey.eventId}`);
            await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
        }
    });
    expect(actionSample(diagnostics, 'event detail alias route remounts').eventDetails).toBeLessThanOrEqual(10);
    expect(actionSample(diagnostics, 'event detail alias route remounts').stackDetails).toBeLessThanOrEqual(10);

    await measureAction(diagnostics, 'stack detail discovery route remounts', async () => {
        for (let index = 0; index < 5; index++) {
            await page.goto(`/next/stack/${journey.stackId}`);
            await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
        }
    });
    expect(actionSample(diagnostics, 'stack detail discovery route remounts').eventDetails).toBeLessThanOrEqual(10);
    expect(actionSample(diagnostics, 'stack detail discovery route remounts').stackDetails).toBeLessThanOrEqual(20);
    expect(actionSample(diagnostics, 'stack detail discovery route remounts').stackEvents).toBe(5);

    await measureAction(diagnostics, 'canonical stack event route remounts', async () => {
        for (let index = 0; index < 5; index++) {
            await page.goto(`/next/stack/${journey.stackId}/event/${journey.eventId}`);
            await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
        }
    });
    expect(actionSample(diagnostics, 'canonical stack event route remounts').eventDetails).toBeLessThanOrEqual(10);
    expect(actionSample(diagnostics, 'canonical stack event route remounts').stackDetails).toBeLessThanOrEqual(10);

    await measureAction(diagnostics, 'full detail navigation and visibility churn', async () => {
        const olderEvent = page.getByRole('button', { name: 'Older event' });
        const newerEvent = page.getByRole('button', { name: 'Newer event' });
        for (let index = 0; index < 5; index++) {
            await expect(olderEvent).toBeEnabled();
            await olderEvent.click();
            await expect(newerEvent).toBeEnabled();
            await newerEvent.click();
        }

        for (let index = 0; index < 30; index++) {
            await setDocumentHidden(page, true);
            await setDocumentHidden(page, false);
        }

        await page.waitForTimeout(2_000);
    });
    // Ten explicit event changes plus at most one visibility-driven refetch.
    expect(actionSample(diagnostics, 'full detail navigation and visibility churn').eventDetails).toBeLessThanOrEqual(11);
    expect(actionSample(diagnostics, 'full detail navigation and visibility churn').stackDetails).toBeLessThanOrEqual(2);
    expect(actionSample(diagnostics, 'full detail navigation and visibility churn').eventList).toBe(0);

    await testInfo.attach('event-effect-chaos-diagnostics', {
        body: Buffer.from(JSON.stringify(diagnostics, null, 2)),
        contentType: 'application/json'
    });
    expect(diagnostics.runtimeErrors).toEqual([]);
    expect(diagnostics.networkFailures).toEqual([]);
    expect(diagnostics.requestFailures).toEqual([]);
    await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
});

function actionSample(diagnostics: RuntimeDiagnostics, name: string): ActionSample {
    const sample = diagnostics.actionSamples.find((candidate) => candidate.name === name);
    if (!sample) {
        throw new Error(`Missing action sample: ${name}`);
    }

    return sample;
}

async function clickAndWaitForPage(
    page: Page,
    organizationId: string,
    buttonName: string,
    expectedPage: number,
    waitForNetwork: boolean = false
): Promise<void> {
    const response = waitForNetwork ? page.waitForResponse((candidate) => isEventListResponse(candidate, organizationId)) : undefined;

    await page.getByRole('button', { name: buttonName }).click();
    await expect(
        page
            .getByText(new RegExp(`^Page ${expectedPage} of`))
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    if (response) {
        expect((await response).ok()).toBe(true);
    }
}

function createGroupedEvent(appUrl: string, message: string, run: string, index: number): { event: Record<string, unknown>; referenceId: string } {
    const referenceId = `pw-event-effects-${run}-${index}`;
    return {
        event: createRepresentativeEvent({
            appUrl,
            message,
            referenceId,
            runId: run
        }),
        referenceId
    };
}

function emptyRequestCounts(): RequestCounts {
    return {
        eventCount: 0,
        eventDetails: 0,
        eventList: 0,
        savedViews: 0,
        stackDetails: 0,
        stackEvents: 0
    };
}

function isEventListRequest(request: Request, organizationId: string): boolean {
    const url = new URL(request.url());
    return url.pathname === `/api/v2/organizations/${organizationId}/events` && url.searchParams.get('mode') === 'summary';
}

function isEventListResponse(response: Response, organizationId: string): boolean {
    return isEventListRequest(response.request(), organizationId);
}

async function measureAction(diagnostics: RuntimeDiagnostics, name: string, action: () => Promise<void>): Promise<void> {
    diagnostics.activeAction = name;
    const initialRequests = { ...diagnostics.requests };
    const initialRequestFailures = diagnostics.requestFailures.length;
    const initialRuntimeErrors = diagnostics.runtimeErrors.length;

    await action();

    diagnostics.actionSamples.push({
        eventCount: diagnostics.requests.eventCount - initialRequests.eventCount,
        eventDetails: diagnostics.requests.eventDetails - initialRequests.eventDetails,
        eventList: diagnostics.requests.eventList - initialRequests.eventList,
        name,
        requestFailures: diagnostics.requestFailures.length - initialRequestFailures,
        runtimeErrors: diagnostics.runtimeErrors.length - initialRuntimeErrors,
        stackDetails: diagnostics.requests.stackDetails - initialRequests.stackDetails,
        stackEvents: diagnostics.requests.stackEvents - initialRequests.stackEvents
    });
}

function recordConsoleMessage(diagnostics: RuntimeDiagnostics, message: ConsoleMessage): void {
    const text = message.text();
    if (message.type() === 'error' && /effect_update_depth_exceeded|maximum update depth|svelte\.dev\/e\/effect/i.test(text)) {
        diagnostics.runtimeErrors.push({ action: diagnostics.activeAction, message: text });
    }
}

function recordNetworkFailure(diagnostics: RuntimeDiagnostics, response: Response): void {
    if (response.status() >= 400) {
        diagnostics.networkFailures.push({
            action: diagnostics.activeAction,
            status: response.status(),
            url: response.url()
        });
    }
}

function recordRequest(diagnostics: RuntimeDiagnostics, request: Request, organizationId: string): void {
    const url = new URL(request.url());
    if (url.pathname.startsWith(`/api/v2/organizations/${organizationId}/saved-views`)) {
        diagnostics.requests.savedViews++;
        return;
    }

    if (isEventListRequest(request, organizationId)) {
        diagnostics.requests.eventList++;
    } else if (url.pathname === `/api/v2/organizations/${organizationId}/events/count`) {
        diagnostics.requests.eventCount++;
    } else if (/^\/api\/v2\/events\/[a-f0-9]{24}$/.test(url.pathname)) {
        diagnostics.requests.eventDetails++;
    } else if (/^\/api\/v2\/stacks\/[a-f0-9]{24}\/events$/.test(url.pathname)) {
        diagnostics.requests.stackEvents++;
    } else if (/^\/api\/v2\/stacks\/[a-f0-9]{24}$/.test(url.pathname)) {
        diagnostics.requests.stackDetails++;
    }
}

function recordRequestFailure(diagnostics: RuntimeDiagnostics, request: Request): void {
    const path = new URL(request.url()).pathname;
    const error = request.failure()?.errorText ?? null;
    const isDeliberateRemount = diagnostics.activeAction.endsWith('route remounts');
    const isCanceledProjectRead = request.method() === 'GET' && /^\/api\/v2\/projects\/[a-f0-9]{24}$/.test(path) && error === 'net::ERR_ABORTED';
    // Repeated page.goto calls intentionally tear down detail observers; Chromium reports their superseded project reads as aborted.
    if (!path.startsWith('/api/v2/') || (isDeliberateRemount && isCanceledProjectRead)) {
        return;
    }

    diagnostics.requestFailures.push({
        action: diagnostics.activeAction,
        error,
        method: request.method(),
        url: request.url()
    });
}

async function setDocumentHidden(page: Page, hidden: boolean): Promise<void> {
    await page.evaluate((nextHidden) => {
        Object.defineProperty(document, 'hidden', {
            configurable: true,
            get: () => nextHidden
        });
        Object.defineProperty(document, 'visibilityState', {
            configurable: true,
            get: () => (nextHidden ? 'hidden' : 'visible')
        });
        document.dispatchEvent(new Event('visibilitychange'));
        window.dispatchEvent(new Event('visibilitychange'));
    }, hidden);
}

async function waitForEventListQuiescence(page: Page, diagnostics: RuntimeDiagnostics): Promise<void> {
    const quietPeriodMs = EVENT_NOTIFICATION_TRAILING_REFRESH_MS + 500;
    const timeoutAt = Date.now() + 30_000;
    let lastRequestCount = diagnostics.requests.eventList;
    let quietSince = Date.now();

    while (Date.now() < timeoutAt) {
        await page.waitForTimeout(250);

        if (diagnostics.requests.eventList !== lastRequestCount) {
            lastRequestCount = diagnostics.requests.eventList;
            quietSince = Date.now();
            continue;
        }

        if (Date.now() - quietSince >= quietPeriodMs) {
            return;
        }
    }

    throw new Error('Event list requests did not become quiet after seeding');
}
