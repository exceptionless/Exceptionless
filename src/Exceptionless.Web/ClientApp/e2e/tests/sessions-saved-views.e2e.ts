import type { Page } from '@playwright/test';

import { mkdirSync } from 'node:fs';
import path from 'node:path';

import { createReferenceId, expect, test } from '../fixtures/e2e-test';
import { getVisibleRow } from '../support/page-helpers';
import { createSessionEvent } from '../support/synthetic-event';

test('Sessions saved views persist active state, display settings, filters, and columns', async ({ e2eApi, e2eScenario, page, request }) => {
    const failedApiRequests: string[] = [];
    page.on('response', (response) => {
        const url = new URL(response.url());
        if (url.pathname.startsWith('/api/') && response.status() >= 400) {
            failedApiRequests.push(`${response.status()} ${url.pathname}`);
        }
    });
    await page.setViewportSize({ height: 900, width: 1280 });

    const sessionId = createReferenceId(e2eScenario.run, '-saved-session');
    const identity = `saved-session-${e2eScenario.run}@exceptionless.test`;
    const name = `Saved Session User ${e2eScenario.run}`;
    const viewName = `E2E Sessions ${e2eScenario.run.slice(-32)}`;
    const viewSlug = savedViewSlug(viewName);
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    let savedSummaryWidth = 0;

    await test.step('seed and open an active session', async () => {
        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, createSessionEvent({ identity, name, sessionId }));
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, sessionId);

        await page.goto('/next/sessions?time=all');
        await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible();
        await expect(page.getByRole('button', { name: /^Type\b/ })).toHaveCount(0);
        await expect(getVisibleRow(page, name, identity)).toBeVisible({ timeout: 30_000 });
        await expect(page.getByRole('columnheader')).toHaveText(['', 'Summary', 'Duration', 'User', 'Date']);

        await page.getByRole('button', { name: /^Manage filters/ }).click();
        const filterSearch = page.getByPlaceholder('Search...').filter({ visible: true });
        await filterSearch.fill('Type');
        await expect(page.getByText('Type', { exact: true })).toHaveCount(0);
        await page.keyboard.press('Escape');
    });

    await test.step('represent View Active as a reload-safe URL override', async () => {
        const viewActive = page.getByRole('switch', { name: 'View Active' });
        await viewActive.click();
        await expect(viewActive).toBeChecked();
        await expect(page).toHaveURL(/[?&]filters=/);
        await expect(getVisibleRow(page, name, identity)).toBeVisible();

        await page.reload();
        await expect(viewActive).toBeChecked();
        await expect(getVisibleRow(page, name, identity)).toBeVisible({ timeout: 30_000 });
    });

    await test.step('configure display and columns, then save the complete view', async () => {
        await openViewMenu(page);
        await page.getByRole('menuitemcheckbox', { name: 'Chart' }).click();
        await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();

        const columnDialog = page.getByRole('dialog', { name: 'Column Picker' });
        await columnDialog.getByRole('button', { name: 'Remove Duration column' }).click();
        await columnDialog.getByRole('checkbox', { name: 'User wrap text' }).click();
        await columnDialog.getByRole('button', { name: 'Move Date up' }).click();
        await columnDialog.getByRole('button', { name: 'Done' }).click();

        await expect(page.getByRole('columnheader', { name: 'Duration' })).toHaveCount(0);
        await expectColumnBefore(page, 'Date', 'User');

        const summaryHeader = page.getByRole('columnheader', { name: 'Summary' });
        const originalSummaryWidth = (await summaryHeader.boundingBox())?.width ?? 0;
        await page.getByRole('button', { name: 'Resize summary column' }).focus();
        await page.keyboard.press('ArrowRight');
        await page.keyboard.press('ArrowRight');
        savedSummaryWidth = (await summaryHeader.boundingBox())?.width ?? 0;
        expect(savedSummaryWidth).toBeGreaterThanOrEqual(originalSummaryWidth + 30);

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Save As...' }).click();
        const saveDialog = page.getByRole('dialog', { name: 'Save View' });
        await saveDialog.getByLabel('Name', { exact: true }).fill(viewName);
        await saveDialog.getByRole('button', { name: 'Save' }).click();
        await expect(saveDialog).toBeHidden({ timeout: 30_000 });

        await expect(page).toHaveURL(new RegExp(`/next/sessions/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
        await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
        await expect
            .poll(
                async () => {
                    const response = await request.get(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views/sessions`, {
                        headers: authorizationHeaders
                    });
                    const views = response.ok() ? ((await response.json()) as { name: string }[]) : [];
                    return views.some((view) => view.name === viewName);
                },
                { timeout: 30_000 }
            )
            .toBe(true);
    });

    await test.step('reload the saved route and verify the complete server representation', async () => {
        await page.reload();
        await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
        await expect(page.getByRole('switch', { name: 'View Active' })).toBeChecked();
        await expect(page.getByRole('columnheader', { name: 'Duration' })).toHaveCount(0);
        await expectColumnBefore(page, 'Date', 'User');
        await expect.poll(async () => (await page.getByRole('columnheader', { name: 'Summary' }).boundingBox())?.width ?? 0).toBeCloseTo(savedSummaryWidth, 0);

        await captureEvidence(page, '01-sessions-saved-view.png');

        await openViewMenu(page);
        await expect(page.getByRole('menuitemcheckbox', { name: 'Chart' })).not.toBeChecked();
        await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();
        const columnDialog = page.getByRole('dialog', { name: 'Column Picker' });
        await expect(columnDialog.getByRole('checkbox', { name: 'User wrap text' })).toBeChecked();
        await captureEvidence(page, '02-sessions-column-management.png');
        await columnDialog.getByRole('button', { name: 'Done' }).click();

        await expect
            .poll(async () => {
                const response = await request.get(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views/sessions`, {
                    headers: authorizationHeaders
                });
                const views = (await response.json()) as {
                    columns?: Record<string, { position?: number; visible?: boolean; wrap?: boolean }>;
                    filter_definitions?: string;
                    name: string;
                    show_chart?: boolean;
                    time?: string;
                    view_type: string;
                }[];
                return views.find((view) => view.name === viewName);
            })
            .toMatchObject({
                columns: {
                    duration: { visible: false },
                    user: { wrap: true }
                },
                show_chart: false,
                view_type: 'sessions'
            });

        const savedViewsResponse = await request.get(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views/sessions`, {
            headers: authorizationHeaders
        });
        const savedViews = (await savedViewsResponse.json()) as { filter_definitions?: string; name: string }[];
        const savedView = savedViews.find((view) => view.name === viewName);
        const savedFilterDefinitions = JSON.parse(savedView?.filter_definitions ?? '[]') as { hidden?: boolean; term?: string; type: string }[];
        expect(savedFilterDefinitions).toContainEqual({
            hidden: true,
            term: 'data.sessionend',
            type: 'boolean'
        });
        expect(savedFilterDefinitions).not.toContainEqual(expect.objectContaining({ type: 'type' }));
    });

    await test.step('temporary changes remain in the URL and Reset to Saved restores active state', async () => {
        const viewActive = page.getByRole('switch', { name: 'View Active' });
        await viewActive.click();
        await expect(viewActive).not.toBeChecked();
        await expect(page).toHaveURL(/[?&]filters=(?:&|$)/);
        await expect(page.getByLabel('Unsaved view changes')).toBeVisible();

        await page.reload();
        await expect(viewActive).not.toBeChecked();
        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Reset to Saved' }).click();
        await expect(page).not.toHaveURL(/[?&]filters=/);
        await expect(viewActive).toBeChecked();
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
        await captureEvidence(page, '03-sessions-reset-to-saved.png');
    });

    expect(failedApiRequests).toEqual([]);
});

test('Sessions legacy raw-filter views keep URL removals and session-scoped statistics', async ({ e2eScenario, page, request }) => {
    await page.setViewportSize({ height: 900, width: 1600 });

    const suffix = e2eScenario.run.slice(-32);
    const viewName = `E2E Legacy Sessions ${suffix}`;
    const viewSlug = savedViewSlug(viewName);
    const rawFilter = 'type:error';
    const statisticsFilters: string[] = [];
    const statisticsPath = `/api/v2/organizations/${e2eScenario.organizationId}/events/count`;

    page.on('request', (eventRequest) => {
        const url = new URL(eventRequest.url());
        if (url.pathname === statisticsPath && url.searchParams.get('aggregations')?.includes('avg:value')) {
            statisticsFilters.push(url.searchParams.get('filter') ?? '');
        }
    });

    const response = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            filter: rawFilter,
            name: viewName,
            organization_id: e2eScenario.organizationId,
            show_chart: true,
            show_stats: true,
            slug: viewSlug,
            time: '[now-7d TO now]',
            view_type: 'sessions'
        },
        headers: { Authorization: `Bearer ${e2eScenario.userToken}` }
    });
    expect(response.status(), await response.text()).toBe(201);

    await page.goto(`/next/sessions/${viewSlug}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    const rawFilterButton = page
        .getByRole('button', { name: new RegExp(`^Raw Filter\\s+${escapeRegExp(rawFilter)}`) })
        .filter({ visible: true })
        .first();
    await expect(rawFilterButton).toBeVisible();
    await expect.poll(() => statisticsFilters).toContain(`type:session AND (${rawFilter})`);

    await rawFilterButton.focus();
    await page.keyboard.press('Enter');
    await page.getByRole('button', { name: 'Remove filter' }).click();
    await expect(page).toHaveURL(/[?&]filters=(?:&|$)/);
    await expect(rawFilterButton).toHaveCount(0);
    await expect.poll(() => statisticsFilters).toContain('type:session');

    await page.reload();
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect(rawFilterButton).toHaveCount(0);
    await expect(page).toHaveURL(/[?&]filters=(?:&|$)/);
});

test('Sessions ignore legacy structured Type filters and Type URL parameters', async ({ e2eScenario, page, request }) => {
    const suffix = e2eScenario.run.slice(-32);
    const viewName = `E2E Structured Sessions ${suffix}`;
    const viewSlug = savedViewSlug(viewName);
    const sessionFilters: string[] = [];
    const sessionsPath = `/api/v2/organizations/${e2eScenario.organizationId}/events/sessions`;

    page.on('request', (eventRequest) => {
        const url = new URL(eventRequest.url());
        if (url.pathname === sessionsPath) {
            sessionFilters.push(url.searchParams.get('filter') ?? '');
        }
    });

    const response = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            filter: 'type:error',
            filter_definitions: JSON.stringify([{ hidden: true, type: 'type', value: ['error'] }]),
            name: viewName,
            organization_id: e2eScenario.organizationId,
            show_chart: true,
            show_stats: true,
            slug: viewSlug,
            time: '[now-7d TO now]',
            view_type: 'sessions'
        },
        headers: { Authorization: `Bearer ${e2eScenario.userToken}` }
    });
    expect(response.status(), await response.text()).toBe(201);

    await page.goto(`/next/sessions/${viewSlug}?type=log`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect(page.getByRole('button', { name: /^Type\b/ })).toHaveCount(0);
    await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
    await expect.poll(() => sessionFilters).toContain('type:session');

    await page.getByRole('button', { name: /^Manage filters/ }).click();
    const filterSearch = page.getByPlaceholder('Search...').filter({ visible: true });
    await filterSearch.fill('Type');
    await expect(page.getByText('Type', { exact: true })).toHaveCount(0);
});

async function captureEvidence(page: Page, fileName: string): Promise<void> {
    const outputDirectory = process.env.DOGFOOD_OUTPUT;
    if (!outputDirectory) return;

    const resolvedDirectory = path.resolve(outputDirectory);
    mkdirSync(resolvedDirectory, { recursive: true });
    await page.screenshot({ path: path.join(resolvedDirectory, fileName) });
}

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function expectColumnBefore(page: Page, firstColumn: string, secondColumn: string): Promise<void> {
    await expect
        .poll(async () => {
            const headings = await page.getByRole('columnheader').allTextContents();
            const firstIndex = headings.findIndex((heading) => heading.trim().startsWith(firstColumn));
            const secondIndex = headings.findIndex((heading) => heading.trim().startsWith(secondColumn));
            return firstIndex >= 0 && secondIndex >= 0 && firstIndex < secondIndex;
        })
        .toBe(true);
}

async function openViewMenu(page: Page): Promise<void> {
    await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
}

function savedViewSlug(value: string): string {
    return value
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '')
        .replace(/-+/g, '-');
}
