import type { Page, Request } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';

interface RequestCounts {
    eventList: number;
    eventStats: number;
    savedViews: number;
    stackList: number;
    stackStats: number;
}

test('stack and event queries reuse fresh parameterized data during in-app navigation', async ({ e2eApi, page }) => {
    const userToken = await e2eApi.login();
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

    const requestCounts: RequestCounts = {
        eventList: 0,
        eventStats: 0,
        savedViews: 0,
        stackList: 0,
        stackStats: 0
    };
    const failedApiRequests: string[] = [];
    const runtimeErrors: string[] = [];
    page.on('console', (message) => {
        if (message.type() === 'error' && /effect_update_depth_exceeded|maximum update depth|svelte\.dev\/e\/effect/i.test(message.text())) {
            runtimeErrors.push(message.text());
        }
    });
    page.on('pageerror', (error) => runtimeErrors.push(error.stack ?? error.message));
    page.on('request', (request) => recordListRequest(requestCounts, request));
    page.on('requestfailed', (request) => {
        if (isApiRequest(request)) {
            failedApiRequests.push(request.url());
        }
    });

    await navigateToList(page, 'Stacks');
    await expect.poll(() => requestCounts.stackList).toBe(1);
    await expect.poll(() => requestCounts.stackStats).toBe(1);
    await expect.poll(() => requestCounts.savedViews).toBe(2);

    await navigateToList(page, 'Events');
    await expect.poll(() => requestCounts.eventList).toBe(1);
    await expect.poll(() => requestCounts.eventStats).toBe(1);
    await expect.poll(() => requestCounts.savedViews).toBe(3);
    const initialRequestCounts = { ...requestCounts };

    for (let index = 0; index < 10; index++) {
        await navigateToList(page, 'Stacks');
        await navigateToList(page, 'Events');
    }

    expect(requestCounts).toEqual(initialRequestCounts);

    await navigateToStackView(page, 'Most Frequent Errors', 'most-frequent-errors');
    await expect.poll(() => requestCounts.stackList).toBe(initialRequestCounts.stackList + 1);
    await expect.poll(() => requestCounts.stackStats).toBe(initialRequestCounts.stackStats + 1);
    const initialStackViewRequestCounts = { ...requestCounts };

    for (let index = 0; index < 10; index++) {
        await navigateToStackView(page, 'All', 'all');
        await navigateToStackView(page, 'Most Frequent Errors', 'most-frequent-errors');
    }

    expect(requestCounts).toEqual(initialStackViewRequestCounts);
    expect(failedApiRequests).toEqual([]);
    expect(runtimeErrors).toEqual([]);
});

function isApiRequest(request: Request): boolean {
    return new URL(request.url()).pathname.startsWith('/api/v2/');
}

function isListRequest(request: Request): boolean {
    return /^\/api\/v2\/organizations\/[^/]+\/events(?:\/count)?$/.test(new URL(request.url()).pathname);
}

async function navigateToList(page: Page, name: 'Events' | 'Stacks'): Promise<void> {
    if (page.url() === 'about:blank') {
        await page.goto(`/next/${name.toLowerCase().replace(/s$/, '')}/all`);
    } else {
        const directLink = page.getByRole('link', { exact: true, name });
        if ((await directLink.count()) > 0) {
            await directLink.click();
        } else {
            const allLink = page.locator(`a[href="/next/${name.toLowerCase().replace(/s$/, '')}/all"]`);
            if (!(await allLink.isVisible())) {
                await page.getByRole('button', { exact: true, name }).click();
            }

            await allLink.click();
        }
    }

    const path = name.toLowerCase().replace(/s$/, '');
    await expect(page).toHaveURL(new RegExp(`/next/${path}(?:/all)?(?:[?#]|$)`));
    await waitForListRefresh(page);
}

async function navigateToStackView(page: Page, name: string, slug: string): Promise<void> {
    const link = page.locator(`a[href="/next/stack/${slug}"]`);
    if (!(await link.isVisible())) {
        await page.getByRole('button', { exact: true, name: 'Stacks' }).click();
    }

    await link.click();
    await expect(page).toHaveURL(new RegExp(`/next/stack/${slug}(?:[?#]|$)`));
    await expect(page.getByRole('heading', { exact: true, name })).toBeVisible();
    await waitForListRefresh(page);
}

function recordListRequest(counts: RequestCounts, request: Request): void {
    const url = new URL(request.url());
    if (/^\/api\/v2\/organizations\/[^/]+\/saved-views(?:\/[^/]+)?$/.test(url.pathname)) {
        counts.savedViews++;
        return;
    }

    if (!isListRequest(request)) {
        return;
    }

    const isStats = url.pathname.endsWith('/count');
    const mode = url.searchParams.get('mode');
    if (mode === 'stack') {
        counts[isStats ? 'stackStats' : 'stackList']++;
    } else if (isStats || mode === 'summary') {
        counts[isStats ? 'eventStats' : 'eventList']++;
    }
}

async function waitForListRefresh(page: Page): Promise<void> {
    const refreshIcon = page.getByTitle('Refresh results').locator('svg');
    await expect(refreshIcon).toBeVisible();
    await expect(refreshIcon).not.toHaveClass(/animate-spin/);
}
