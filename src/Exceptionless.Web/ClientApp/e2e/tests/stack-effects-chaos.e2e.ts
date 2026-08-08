import type { ConsoleMessage, Page, Request, Response } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { createRepresentativeEvent } from '../support/synthetic-event';

interface ActionSample {
    listRequests: number;
    name: string;
    runtimeErrors: number;
}

interface RuntimeDiagnostics {
    actionSamples: ActionSample[];
    activeAction: string;
    listRequests: number;
    networkFailures: { action: string; status: number; url: string }[];
    runtimeErrors: { action: string; message: string }[];
}

test('stack effects stay bounded through background, paging, and navigation chaos @signup', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    test.slow();

    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const diagnostics: RuntimeDiagnostics = {
        actionSamples: [],
        activeAction: 'setup',
        listRequests: 0,
        networkFailures: [],
        runtimeErrors: []
    };

    page.on('console', (message) => recordConsoleError(diagnostics, message));
    page.on('pageerror', (error) => diagnostics.runtimeErrors.push({ action: diagnostics.activeAction, message: error.stack ?? error.message }));
    page.on('request', (request) => recordListRequest(diagnostics, request, e2eScenario.organizationId));
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

    await measureAction(diagnostics, 'selected stack refresh', async () => {
        const rowSelection = page.getByRole('checkbox', { name: 'Select row' }).first();
        await rowSelection.click();
        await expect(rowSelection).toBeChecked();

        const response = page.waitForResponse((candidate) => isStackListResponse(candidate, e2eScenario.organizationId));
        await page.getByTitle('Return to the first page to refresh results').click();
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
    expect(actionSample(diagnostics, 'background ingestion and resume').listRequests).toBeLessThanOrEqual(1);

    await measureAction(diagnostics, 'bursty stack change notifications', async () => {
        await page.evaluate((organizationId) => {
            for (let index = 0; index < 30; index++) {
                document.dispatchEvent(
                    new CustomEvent('StackChanged', {
                        detail: {
                            change_type: 2,
                            data: {},
                            id: `chaos-missing-stack-${index}`,
                            organization_id: organizationId,
                            type: 'Stack'
                        }
                    })
                );
            }
        }, e2eScenario.organizationId);
        await page.waitForTimeout(2_000);
    });
    expect(actionSample(diagnostics, 'bursty stack change notifications').listRequests).toBe(1);

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
    const initialRuntimeErrors = diagnostics.runtimeErrors.length;

    await action();

    diagnostics.actionSamples.push({
        listRequests: diagnostics.listRequests - initialListRequests,
        name,
        runtimeErrors: diagnostics.runtimeErrors.length - initialRuntimeErrors
    });
}

function recordConsoleError(diagnostics: RuntimeDiagnostics, message: ConsoleMessage): void {
    const text = message.text();
    if (message.type() === 'error' && /effect_update_depth_exceeded|maximum update depth|svelte\.dev\/e\/effect/i.test(text)) {
        diagnostics.runtimeErrors.push({ action: diagnostics.activeAction, message: text });
    }
}

function recordListRequest(diagnostics: RuntimeDiagnostics, request: Request, organizationId: string): void {
    if (isStackListRequest(request, organizationId)) {
        diagnostics.listRequests++;
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
