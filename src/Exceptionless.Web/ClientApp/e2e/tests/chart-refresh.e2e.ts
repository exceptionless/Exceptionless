import type { Page, Route } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';

test('dashboard charts stay mounted while list data refreshes', async ({ e2eApi, page }) => {
    const userToken = await e2eApi.login();
    await e2eApi.updateProductTour(userToken, 'app-welcome', 1, 2);
    const organizations = await e2eApi.getOrganizations(userToken);
    const organizationId = organizations[0]?.id;
    expect(organizationId).toBeTruthy();

    await page.addInitScript(
        ({ organizationId, token }) => {
            window.localStorage.setItem('satellizer_token', token);
            window.localStorage.setItem('organization', JSON.stringify(organizationId));
        },
        { organizationId, token: userToken }
    );

    await verifyChartRefresh(page, '/next/stack/all', (route) => isOrganizationEventListRequest(route, organizationId!, 'stack_frequent'));
    await verifyChartRefresh(page, '/next/event/all', (route) => isOrganizationEventListRequest(route, organizationId!, 'summary'));
    await verifyChartRefresh(page, '/next/sessions/all', (route) => {
        return new URL(route.request().url()).pathname === `/api/v2/organizations/${organizationId}/events/sessions`;
    });
});

function isOrganizationEventListRequest(route: Route, organizationId: string, mode: string): boolean {
    const url = new URL(route.request().url());
    return url.pathname === `/api/v2/organizations/${organizationId}/events` && url.searchParams.get('mode') === mode;
}

async function verifyChartRefresh(page: Page, path: string, matchesRefreshRequest: (route: Route) => boolean): Promise<void> {
    await page.goto(path);
    const chart = page.locator('[data-slot="chart"]').first();
    await expect(chart).toBeVisible();

    const chartElement = await chart.elementHandle();
    expect(chartElement).not.toBeNull();

    let releaseRefresh: () => void = () => {};
    const refreshReleased = new Promise<void>((resolve) => {
        releaseRefresh = resolve;
    });
    let markRefreshIntercepted: () => void = () => {};
    const refreshIntercepted = new Promise<void>((resolve) => {
        markRefreshIntercepted = resolve;
    });
    let markRefreshFinished: () => void = () => {};
    const refreshFinished = new Promise<void>((resolve) => {
        markRefreshFinished = resolve;
    });
    let refreshWasIntercepted = false;

    const holdRefresh = async (route: Route) => {
        if (!matchesRefreshRequest(route)) {
            await route.fallback();
            return;
        }

        refreshWasIntercepted = true;
        markRefreshIntercepted();
        try {
            await refreshReleased;
            await route.continue();
        } finally {
            markRefreshFinished();
        }
    };

    await page.route('**/api/v2/organizations/**', holdRefresh);
    try {
        await page.getByTitle('Refresh results').click();
        await refreshIntercepted;
        await expect(page.getByTitle('Refresh results').locator('svg')).toHaveClass(/animate-spin/);
        expect(await chartElement!.evaluate((element) => element.isConnected)).toBe(true);
        await expect(chart).toBeVisible();
    } finally {
        releaseRefresh();
        if (refreshWasIntercepted) {
            await refreshFinished;
        }
        await page.unroute('**/api/v2/organizations/**', holdRefresh);
    }

    await expect(page.getByTitle('Refresh results').locator('svg')).not.toHaveClass(/animate-spin/);
    expect(await chartElement!.evaluate((element) => element.isConnected)).toBe(true);
}
