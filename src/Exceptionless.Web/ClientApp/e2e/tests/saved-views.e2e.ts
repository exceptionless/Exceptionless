import type { Page, Request } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

test('home navigation honors personal and organization saved views and survives deletion', async ({ e2eApi, e2eScenario, page }) => {
    const failedApiRequests = captureFailedApiRequests(page);
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const viewName = `E2E Home ${journey.run.slice(-36)}`;
    const viewSlug = savedViewSlug(viewName);

    await test.step('fall back to the first Stacks saved view when no default is configured', async () => {
        await page.goto('/next/');
        await expect(page).toHaveURL(/\/next\/stack\/all(?:[?#]|$)/);
        await expect(page.getByRole('heading', { name: 'All' })).toBeVisible({ timeout: 30_000 });
    });

    await test.step('prefer the personal saved view', async () => {
        await journey.submitRepresentativeEvent();
        await saveView(page, viewName, journey.referenceId, 'all');

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Set as my home view' }).click();
        await expect(page.getByText(`"${viewName}" is now your home view.`)).toBeVisible();

        await page.goto('/next/');
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
    });

    await test.step('fall back to the organization saved view after clearing the personal preference', async () => {
        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Set as organization home' }).click();
        await expect(page.getByText(`"${viewName}" is now the organization home view.`)).toBeVisible();

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Clear my home view' }).click();
        await expect(page.getByText('Personal home view cleared.')).toBeVisible();

        await page.goto('/next/');
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
    });

    await test.step('clear deleted defaults and return to the first Stacks saved view', async () => {
        const deletion = await page.evaluate(
            async ({ organizationId, token }) => {
                const headers = { Authorization: `Bearer ${token}` };
                const defaultsResponse = await fetch(`/api/v2/organizations/${organizationId}/saved-view-defaults`, { headers });
                const defaults = await defaultsResponse.json();
                const response = await fetch(`/api/v2/saved-views/${defaults.organization_default.id}`, {
                    headers,
                    method: 'DELETE'
                });
                const updatedDefaultsResponse = await fetch(`/api/v2/organizations/${organizationId}/saved-view-defaults`, {
                    headers: { Authorization: `Bearer ${token}` }
                });
                return {
                    defaults: await updatedDefaultsResponse.json(),
                    status: response.status
                };
            },
            { organizationId: e2eScenario.organizationId, token: e2eScenario.userToken }
        );
        expect(deletion.status).toBe(202);
        expect(deletion.defaults).not.toHaveProperty('organization_default');
        expect(deletion.defaults).not.toHaveProperty('user_default');

        await page.goto('/next/');
        await expect(page).toHaveURL(/\/next\/stack\/all(?:[?#]|$)/);
    });

    expect(failedApiRequests).toEqual([]);
});

test('events saved view can be saved, renamed, loaded, and deleted', async ({ e2eApi, e2eScenario, page }) => {
    const failedApiRequests = captureFailedApiRequests(page);
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const suffix = journey.run.slice(-36);
    const viewName = `E2E Events ${suffix}`;
    const renamedViewName = `E2E Events Renamed ${suffix}`;
    const viewSlug = savedViewSlug(viewName);

    await test.step('submit a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('save the filtered Events page as a view', async () => {
        await page.goto(`/next/event?reference=${encodeURIComponent(journey.referenceId)}&time=all`);
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Save As...' }).click();

        const dialog = page.getByRole('dialog', { name: 'Save View' });
        await expect(dialog).toBeVisible();
        await dialog.getByLabel('Name', { exact: true }).fill(viewName);
        await expect(dialog.getByLabel('URL name', { exact: true })).toHaveValue(viewSlug);
        await dialog.getByRole('button', { name: 'Save' }).click();
        await expect(dialog).toBeHidden({ timeout: 30_000 });

        await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });

    await test.step('rename the saved view and keep the saved route active', async () => {
        await openViewMenu(page);
        await page.getByRole('menuitem', { exact: true, name: 'Rename' }).click();

        const dialog = page.getByRole('dialog', { name: 'Rename View' });
        await expect(dialog).toBeVisible();
        await dialog.getByLabel('Name', { exact: true }).fill(renamedViewName);
        await dialog.getByLabel('URL name', { exact: true }).fill(viewSlug);
        await dialog.getByRole('button', { name: 'Rename' }).click();
        await expect(dialog).toBeHidden({ timeout: 30_000 });

        await expect(page.getByRole('heading', { name: renamedViewName })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });

    await test.step('persist removal of a saved filter through reload', async () => {
        const referenceFilter = page
            .getByRole('button', { name: new RegExp(`^Reference\\s+${escapeRegExp(journey.referenceId)}`) })
            .filter({ visible: true })
            .first();

        await page.goto(`/next/event/${viewSlug}?reference=`);
        await expect(page).toHaveURL(/[?&]reference=(?:&|$)/);
        await expect(referenceFilter).toHaveCount(0);
        await page.reload();
        await expect(page).toHaveURL(/[?&]reference=(?:&|$)/);
        await expect(referenceFilter).toHaveCount(0);

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Reset to Saved' }).click();
        await expect(page).not.toHaveURL(/[?&]reference=/);
        await expect(referenceFilter).toBeVisible();
    });

    await test.step('reset route-specific filter overrides to the saved view', async () => {
        await page.goto(`/next/event/${viewSlug}?project=${e2eScenario.projectId}`);
        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Reset to Saved' }).click();

        await expect(page.getByRole('menu')).toBeHidden();
        await expect(page).not.toHaveURL(/[?&]project=/);
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });

    await test.step('delete the saved view and return to the default Events view', async () => {
        await openViewMenu(page);
        await page.getByRole('menuitem', { name: `Delete "${renamedViewName}"` }).click();

        const dialog = page.getByRole('alertdialog', { name: 'Delete Saved View' });
        await expect(dialog).toBeVisible();
        await dialog.getByRole('button', { name: 'Delete' }).click();
        await expect(dialog).toBeHidden({ timeout: 30_000 });

        await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(/\/next\/event(?:[?#]|$)/);
        await expect(page.getByRole('heading', { name: renamedViewName })).toHaveCount(0);
    });

    expect(failedApiRequests).toEqual([]);
});

test('switching saved views discards temporary filter overrides', async ({ e2eApi, e2eScenario, page }) => {
    const failedApiRequests = captureFailedApiRequests(page);
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const suffix = journey.run.slice(-28);
    const firstViewName = `E2E First View ${suffix}`;
    const secondViewName = `E2E Second View ${suffix}`;
    const firstViewSlug = savedViewSlug(firstViewName);
    const secondViewSlug = savedViewSlug(secondViewName);

    await journey.submitRepresentativeEvent();
    await saveView(page, firstViewName, journey.referenceId, '15m');
    await saveView(page, secondViewName, journey.referenceId, '1d');

    await page.goto(`/next/event/${firstViewSlug}`);
    const dateFilter = page.getByRole('button', { name: /^Date/ }).filter({ visible: true }).first();
    await dateFilter.click();
    await page.getByRole('button', { name: 'Last 90 days' }).click();
    await expect(page).toHaveURL(/[?&]time=90d(?:&|$)/);

    await page.getByRole('link', { exact: true, name: secondViewName }).first().click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(secondViewSlug)}(?:[?#]|$)`));
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 24 hours/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    expect(failedApiRequests).toEqual([]);
});

function captureFailedApiRequests(page: Page): { error: null | string; method: string; url: string }[] {
    const failures: { error: null | string; method: string; url: string }[] = [];
    page.on('requestfailed', (request: Request) => {
        if (new URL(request.url()).pathname.startsWith('/api/v2/')) {
            failures.push({
                error: request.failure()?.errorText ?? null,
                method: request.method(),
                url: request.url()
            });
        }
    });

    return failures;
}

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
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

async function saveView(page: Page, viewName: string, referenceId: string, time: string): Promise<void> {
    await page.goto(`/next/event?reference=${encodeURIComponent(referenceId)}&time=${time}`);
    await openViewMenu(page);
    await page.getByRole('menuitem', { name: 'Save As...' }).click();
    const dialog = page.getByRole('dialog', { name: 'Save View' });
    await dialog.getByLabel('Name', { exact: true }).fill(viewName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(dialog).toBeHidden({ timeout: 30_000 });
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
}
