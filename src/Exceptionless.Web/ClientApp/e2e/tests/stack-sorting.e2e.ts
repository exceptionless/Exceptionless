import type { Page, Response } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

test('stack sort survives reload and resets pagination and selection without sending an API sort parameter', async ({ e2eApi, e2eScenario, page }) => {
    await ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario).submitRepresentativeEvent();
    const requests: URL[] = [];
    page.on('request', (request) => {
        const url = new URL(request.url());
        if (isStackRequest(url)) {
            requests.push(url);
        }
    });

    const initialResponse = waitForStackMode(page, 'stack_frequent');
    await page.goto(`/next/stack?project=${e2eScenario.projectId}&page=2`);
    expect((await initialResponse).ok()).toBe(true);
    await expect(page.getByRole('button', { name: 'Sort by Events descending' })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: /Sort by First/ })).toHaveCount(0);

    const recentResponse = waitForStackMode(page, 'stack_recent');
    await page.getByRole('button', { name: 'Sort by Last descending' }).click();
    expect((await recentResponse).ok()).toBe(true);
    await expect(page).toHaveURL(/[?&]sort=stack_recent(?:&|$)/);
    await expect(page).not.toHaveURL(/[?&]page=2(?:&|$)/);

    const reloadResponse = waitForStackMode(page, 'stack_recent');
    await page.reload();
    expect((await reloadResponse).ok()).toBe(true);
    await expect(page.getByRole('button', { name: 'Sort by Last descending' })).toHaveAttribute('aria-pressed', 'true');

    await page.getByRole('checkbox', { name: 'Select row' }).first().click();
    await expect(page.getByRole('checkbox', { checked: true, name: 'Select row' })).toHaveCount(1);
    await page.getByRole('button', { name: 'Sort by Events descending' }).click();
    await expect(page).not.toHaveURL(/[?&]sort=/);
    await expect(page.getByRole('button', { name: 'Sort by Events descending' })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('checkbox', { checked: true })).toHaveCount(0);
    expect(requests.length).toBeGreaterThanOrEqual(3);
    expect(requests.every((url) => !url.searchParams.has('sort'))).toBe(true);
});

for (const [sort, label, mode] of [
    ['-events', 'Events', 'stack_frequent'],
    ['-last', 'Last', 'stack_recent']
] as const) {
    test(`legacy ${sort} stack views preserve their sort through reset and save`, async ({ e2eScenario, page, request }) => {
        const headers = { Authorization: `Bearer ${e2eScenario.userToken}` };
        const slug = `sort-${label.toLowerCase()}`;
        const createResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
            data: {
                filter: 'type:error',
                name: `Sort ${label}`,
                organization_id: e2eScenario.organizationId,
                slug,
                sort,
                view_type: 'stacks'
            },
            headers
        });
        expect(createResponse.status()).toBe(201);
        const savedView = (await createResponse.json()) as { id: string };

        const initialResponse = waitForStackMode(page, mode);
        await page.goto(`/next/stack/${slug}`);
        expect((await initialResponse).ok()).toBe(true);
        await expect(page.getByRole('button', { name: `Sort by ${label} descending` })).toHaveAttribute('aria-pressed', 'true');
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);

        await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
        await page.getByRole('menuitemcheckbox', { exact: true, name: 'Chart' }).click();
        const displaySaveResponse = page.waitForResponse(
            (response) => response.url().includes(`/saved-views/${savedView.id}`) && response.request().method() === 'PATCH'
        );
        await page.getByRole('menuitem', { exact: true, name: 'Save' }).click();
        const displayResponse = await displaySaveResponse;
        expect(displayResponse.ok()).toBe(true);
        expect(displayResponse.request().postDataJSON().sort).toBe(sort);
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);

        const otherLabel = label === 'Events' ? 'Last' : 'Events';
        const otherMode = mode === 'stack_frequent' ? 'stack_recent' : 'stack_frequent';
        await page.getByRole('button', { name: `Sort by ${otherLabel} descending` }).click();
        await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
        await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { name: 'Reset to Saved' }).click();
        await expect(page.getByRole('button', { name: `Sort by ${label} descending` })).toHaveAttribute('aria-pressed', 'true');
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);

        await page.getByRole('button', { name: `Sort by ${otherLabel} descending` }).click();
        await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
        const saveResponse = page.waitForResponse(
            (response) => response.url().includes(`/saved-views/${savedView.id}`) && response.request().method() === 'PATCH'
        );
        await page.getByRole('menuitem', { exact: true, name: 'Save' }).click();
        const response = await saveResponse;
        expect(response.ok()).toBe(true);
        expect(response.request().postDataJSON().sort).toBe(otherMode);
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);

        await page.goto(`/next/stack/${slug}`);
        await expect(page.getByRole('button', { name: `Sort by ${otherLabel} descending` })).toHaveAttribute('aria-pressed', 'true');
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
    });
}

function isStackRequest(url: URL): boolean {
    return url.pathname.endsWith('/events') && url.searchParams.get('mode')?.startsWith('stack_') === true;
}

function waitForStackMode(page: Page, mode: string): Promise<Response> {
    return page.waitForResponse((response) => {
        const url = new URL(response.url());
        return isStackRequest(url) && url.searchParams.get('mode') === mode;
    });
}
