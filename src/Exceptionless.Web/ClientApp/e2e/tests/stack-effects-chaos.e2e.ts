import type { ConsoleMessage, Page, Request, Response } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { createRepresentativeEvent } from '../support/synthetic-event';
import { dispatchWebSocketMessages, installWebSocketTestHarness } from '../support/web-socket';

const STACK_NOTIFICATION_TRAILING_REFRESH_MS = 5_000;

interface ActionSample {
    countRequests: number;
    listRequests: number;
    name: string;
    requestFailures: number;
    runtimeErrors: number;
}

interface RuntimeDiagnostics {
    actionSamples: ActionSample[];
    activeAction: string;
    countRequests: number;
    listRequests: number;
    networkFailures: { action: string; status: number; url: string }[];
    requestFailures: { action: string; error: null | string; method: string; url: string }[];
    runtimeErrors: { action: string; message: string }[];
    savedViewRequests: number;
}

test('stack effects stay bounded through background, paging, and navigation chaos @signup', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    test.slow();
    await installWebSocketTestHarness(page);

    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const diagnostics: RuntimeDiagnostics = {
        actionSamples: [],
        activeAction: 'setup',
        countRequests: 0,
        listRequests: 0,
        networkFailures: [],
        requestFailures: [],
        runtimeErrors: [],
        savedViewRequests: 0
    };

    page.on('console', (message) => recordConsoleError(diagnostics, message));
    page.on('pageerror', (error) => diagnostics.runtimeErrors.push({ action: diagnostics.activeAction, message: error.stack ?? error.message }));
    page.on('request', (request) => recordRequest(diagnostics, request, e2eScenario.organizationId));
    page.on('requestfailed', (request) => recordRequestFailure(diagnostics, request));
    page.on('response', (response) => recordNetworkFailure(diagnostics, response));

    await test.step('seed enough independent stacks to exercise pagination', async () => {
        await journey.submitRepresentativeEvent();
        const events = Array.from({ length: 6 }, (_, index) => createChaosEvent(e2eApi.environment.appUrl, e2eScenario.run, index));
        await Promise.all(events.map(({ event }) => e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, event)));
        await Promise.all(events.map(({ referenceId }) => e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId)));
    });

    await test.step('load the stack page with one page of results', async () => {
        const response = page.waitForResponse((candidate) => isStackListResponse(candidate, e2eScenario.organizationId));
        await page.goto('/next/stack?limit=5');
        expect((await response).ok()).toBe(true);
        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible();
        await expect(page.locator('tbody tr:visible').first()).toBeVisible();
        await expect(page.getByRole('button', { name: 'Go to next page' })).toBeEnabled();
    });

    await test.step('open a stack route without aborting detail requests', async () => {
        const failedDetailRequests: { failure: null | string; pageUrl: string; requestUrl: string }[] = [];
        const recordFailedDetailRequest = (request: Request) => {
            if (new URL(request.url()).pathname.startsWith('/api/v2/')) {
                failedDetailRequests.push({
                    failure: request.failure()?.errorText ?? null,
                    pageUrl: page.url(),
                    requestUrl: request.url()
                });
            }
        };

        page.on('requestfailed', recordFailedDetailRequest);

        try {
            await page.goto(`/next/stack/${journey.stackId}`);
            await expect(page).toHaveURL(new RegExp(`/next/stack/${journey.stackId}$`));
            await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
        } finally {
            page.off('requestfailed', recordFailedDetailRequest);
        }

        expect(failedDetailRequests).toEqual([]);
        await page.goto('/next/stack?limit=5');
        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible();
    });

    await measureAction(diagnostics, 'stack timeline drag', async () => {
        await page.locator('tbody tr:visible').first().click();
        const dialog = page.getByRole('dialog');
        await expect(dialog).toBeVisible();
        const timeline = dialog.locator('[data-slot="chart"]');
        await expect(timeline).toBeVisible();

        const bounds = await timeline.boundingBox();
        expect(bounds).not.toBeNull();
        if (bounds) {
            const y = bounds.y + bounds.height / 2;
            await page.mouse.move(bounds.x + bounds.width * 0.2, y);
            await page.waitForTimeout(250);
            await page.mouse.move(bounds.x + bounds.width * 0.3, y);
            await expect(page.locator('.lc-tooltip-root')).toHaveCSS('pointer-events', 'none');
            await page.mouse.down();
            await page.mouse.move(bounds.x + bounds.width * 0.8, y, { steps: 20 });
            await page.mouse.up();
        }

        await page.waitForTimeout(500);
        await page.getByRole('button', { name: 'Close' }).click();
    });
    expect(actionSample(diagnostics, 'stack timeline drag').runtimeErrors).toBe(0);

    await measureAction(diagnostics, 'selected stack refresh', async () => {
        const rowSelection = page.getByRole('checkbox', { name: 'Select row' }).first();
        await rowSelection.click();
        await expect(rowSelection).toBeChecked();

        const response = page.waitForResponse((candidate) => isStackListResponse(candidate, e2eScenario.organizationId));
        await page.getByTitle('Refresh results').click();
        expect((await response).ok()).toBe(true);
        await expect(rowSelection).not.toBeChecked();
    });
    expect(actionSample(diagnostics, 'selected stack refresh').listRequests).toBe(1);

    await measureAction(diagnostics, 'paging', async () => {
        for (let index = 0; index < 4; index++) {
            await page.getByRole('button', { name: 'Go to next page' }).click();
            await expect(page).toHaveURL(/(?:\?|&)page=2(?:&|$)/);
            await page.getByRole('button', { name: 'Go to previous page' }).click();
            await expect(page).not.toHaveURL(/(?:\?|&)page=2(?:&|$)/);
        }
    });
    expect(actionSample(diagnostics, 'paging').listRequests).toBe(1);

    await measureAction(diagnostics, 'stack detail mount and teardown', async () => {
        for (let index = 0; index < 5; index++) {
            await page.locator('tbody tr:visible').first().click();
            await expect(page.getByRole('dialog')).toBeVisible();
            await page.getByRole('button', { name: 'Close' }).click();
            await expect(page.getByRole('dialog')).not.toBeVisible();
        }
    });
    expect(actionSample(diagnostics, 'stack detail mount and teardown').listRequests).toBe(0);

    await measureAction(diagnostics, 'rapid visibility changes', async () => {
        for (let index = 0; index < 30; index++) {
            await setDocumentHidden(page, true);
            await setDocumentHidden(page, false);
        }

        await page.waitForTimeout(2_000);
    });
    expect(actionSample(diagnostics, 'rapid visibility changes').listRequests).toBeLessThanOrEqual(1);

    await measureAction(diagnostics, 'background ingestion and resume', async () => {
        await setDocumentHidden(page, true);
        const { event, referenceId } = createChaosEvent(e2eApi.environment.appUrl, e2eScenario.run, 100);
        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, event);
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId);
        await setDocumentHidden(page, false);
        await page.waitForTimeout(2_000);
    });
    expect(actionSample(diagnostics, 'background ingestion and resume').listRequests).toBeLessThanOrEqual(2);

    await page.waitForTimeout(STACK_NOTIFICATION_TRAILING_REFRESH_MS);
    await measureAction(diagnostics, 'removal notification leading window', async () => {
        await dispatchWebSocketMessages(page, [
            {
                message: {
                    change_type: 2,
                    data: {},
                    id: 'chaos-removed-stack',
                    organization_id: e2eScenario.organizationId,
                    project_id: e2eScenario.projectId,
                    type: 'Stack'
                },
                type: 'StackChanged'
            }
        ]);
        await page.waitForTimeout(2_000);
    });
    expect(actionSample(diagnostics, 'removal notification leading window').countRequests).toBe(0);
    expect(actionSample(diagnostics, 'removal notification leading window').listRequests).toBe(0);

    await measureAction(diagnostics, 'removal notification trailing reconciliation', async () => {
        await page.waitForTimeout(STACK_NOTIFICATION_TRAILING_REFRESH_MS - 1_500);
    });
    expect(actionSample(diagnostics, 'removal notification trailing reconciliation').countRequests).toBe(1);
    expect(actionSample(diagnostics, 'removal notification trailing reconciliation').listRequests).toBe(1);

    await measureAction(diagnostics, 'sustained stack change notifications', async () => {
        for (let wave = 0; wave < 4; wave++) {
            await dispatchWebSocketMessages(
                page,
                Array.from({ length: 30 }, (_, index) => ({
                    message: {
                        change_type: 1,
                        data: {},
                        id: `chaos-missing-stack-${wave}-${index}`,
                        organization_id: e2eScenario.organizationId,
                        project_id: e2eScenario.projectId,
                        type: 'Stack'
                    },
                    type: 'StackChanged'
                }))
            );
            await page.waitForTimeout(1_600);
        }

        await page.waitForTimeout(2_000);
    });
    const sustainedNotificationSample = actionSample(diagnostics, 'sustained stack change notifications');
    expect(sustainedNotificationSample.countRequests).toBeGreaterThanOrEqual(1);
    expect(sustainedNotificationSample.countRequests).toBeLessThanOrEqual(2);
    expect(sustainedNotificationSample.listRequests).toBeGreaterThanOrEqual(1);
    expect(sustainedNotificationSample.listRequests).toBeLessThanOrEqual(2);
    expect(sustainedNotificationSample.runtimeErrors).toBe(0);

    await measureAction(diagnostics, 'route remounts', async () => {
        for (let index = 0; index < 5; index++) {
            await page.goto(`/next/event/${journey.eventId}`);
            await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
            await page.goto('/next/stack?limit=5');
            await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible();
        }
    });
    expect(actionSample(diagnostics, 'route remounts').listRequests).toBeLessThanOrEqual(5);

    await testInfo.attach('stack-effect-chaos-diagnostics', {
        body: Buffer.from(JSON.stringify(diagnostics, null, 2)),
        contentType: 'application/json'
    });

    expect(diagnostics.runtimeErrors).toEqual([]);
    expect(diagnostics.networkFailures).toEqual([]);
    expect(diagnostics.requestFailures).toEqual([]);
    await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible();
});

function actionSample(diagnostics: RuntimeDiagnostics, name: string): ActionSample {
    const sample = diagnostics.actionSamples.find((candidate) => candidate.name === name);
    expect(sample, `Missing diagnostics for "${name}"`).toBeDefined();
    return sample!;
}

function createChaosEvent(appUrl: string, run: string, index: number): { event: Record<string, unknown>; referenceId: string } {
    const referenceId = `pw-effects-${run}-${index}`;
    const message = `Playwright effect chaos ${run} ${index}`;
    const event = createRepresentativeEvent({
        appUrl,
        message,
        referenceId,
        runId: run
    });
    const data = event.data as Record<string, unknown>;
    const simpleError = data['@simple_error'] as Record<string, unknown>;
    simpleError.type = `PlaywrightEffectChaosException${index}`;
    simpleError.stack_trace = `Error: ${message}\n    at stack-effect-chaos-${index}.ts:${index + 1}:1`;

    return { event, referenceId };
}

function isStackListRequest(request: Request, organizationId: string): boolean {
    const url = new URL(request.url());
    return url.pathname === `/api/v2/organizations/${organizationId}/events` && url.searchParams.get('mode') === 'stack_frequent';
}

function isStackListResponse(response: Response, organizationId: string): boolean {
    return isStackListRequest(response.request(), organizationId);
}

async function measureAction(diagnostics: RuntimeDiagnostics, name: string, action: () => Promise<void>): Promise<void> {
    diagnostics.activeAction = name;
    const initialListRequests = diagnostics.listRequests;
    const initialCountRequests = diagnostics.countRequests;
    const initialRequestFailures = diagnostics.requestFailures.length;
    const initialRuntimeErrors = diagnostics.runtimeErrors.length;

    await action();

    diagnostics.actionSamples.push({
        countRequests: diagnostics.countRequests - initialCountRequests,
        listRequests: diagnostics.listRequests - initialListRequests,
        name,
        requestFailures: diagnostics.requestFailures.length - initialRequestFailures,
        runtimeErrors: diagnostics.runtimeErrors.length - initialRuntimeErrors
    });
}

function recordConsoleError(diagnostics: RuntimeDiagnostics, message: ConsoleMessage): void {
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
    if (new URL(request.url()).pathname.startsWith(`/api/v2/organizations/${organizationId}/saved-views`)) {
        diagnostics.savedViewRequests++;
        return;
    }

    if (isStackListRequest(request, organizationId)) {
        diagnostics.listRequests++;
        return;
    }

    const url = new URL(request.url());
    if (url.pathname === `/api/v2/organizations/${organizationId}/events/count` && url.searchParams.get('mode') === 'stack_frequent') {
        diagnostics.countRequests++;
    }
}

function recordRequestFailure(diagnostics: RuntimeDiagnostics, request: Request): void {
    if (!new URL(request.url()).pathname.startsWith('/api/v2/')) {
        return;
    }

    diagnostics.requestFailures.push({
        action: diagnostics.activeAction,
        error: request.failure()?.errorText ?? null,
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
