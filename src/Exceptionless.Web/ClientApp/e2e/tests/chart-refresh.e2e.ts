import type { Page, Request, Route } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';

test('dashboard charts stay mounted while list data refreshes', async ({ e2eApi, page }) => {
    // Arrange
    const userToken = await e2eApi.login();
    await e2eApi.recordProductTour(userToken, 'app-welcome');
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

    // Act & Assert: the helper refreshes each dashboard and checks that its chart stays mounted.
    await verifyChartRefresh(page, '/stack/all', (route) => isOrganizationEventListRequest(route, organizationId!, 'stack_frequent'));
    await verifyChartRefresh(page, '/event/all', (route) => isOrganizationEventListRequest(route, organizationId!, 'summary'));
    await verifyChartRefresh(page, '/sessions/all', (request) => {
        return new URL(request.url()).pathname === `/api/v2/organizations/${organizationId}/events/sessions`;
    });
});

function isOrganizationEventListRequest(request: Request, organizationId: string, mode: string): boolean {
    const url = new URL(request.url());
    return url.pathname === `/api/v2/organizations/${organizationId}/events` && url.searchParams.get('mode') === mode;
}

async function verifyChartRefresh(page: Page, path: string, matchesRefreshRequest: (request: Request) => boolean): Promise<void> {
    // Arrange
    const initialResponse = page.waitForResponse((response) => matchesRefreshRequest(response.request()));
    await page.goto(path);
    expect((await initialResponse).ok()).toBe(true);
    const chart = page.locator('[data-slot="chart"]').first();
    await expect(chart).toBeVisible();
    await expect(page.getByTitle('Refresh results').locator('svg')).not.toHaveClass(/animate-spin/);

    const chartElement = await chart.elementHandle();
    expect(chartElement).not.toBeNull();

    let releaseRefresh: () => void = () => {};
    const refreshReleased = new Promise<void>((resolve) => {
        releaseRefresh = resolve;
    });
    let markRefreshFinished: () => void = () => {};
    const refreshFinished = new Promise<void>((resolve) => {
        markRefreshFinished = resolve;
    });
    let refreshWasIntercepted = false;

    const holdRefresh = async (route: Route) => {
        if (!matchesRefreshRequest(route.request())) {
            await route.fallback();
            return;
        }

        refreshWasIntercepted = true;
        try {
            await refreshReleased;
            await route.continue();
        } finally {
            markRefreshFinished();
        }
    };

    await page.route('**/api/v2/organizations/**', holdRefresh);
    try {
        // Act
        await page.getByTitle('Refresh results').click();
        await expect.poll(() => refreshWasIntercepted, { message: 'The refresh action must request the dashboard list' }).toBe(true);
        // Assert
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
