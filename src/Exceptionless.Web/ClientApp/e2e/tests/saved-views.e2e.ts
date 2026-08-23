import type { Page, Request } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

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

test('switching saved views preserves each view temporary filter overrides across page reloads', async ({ e2eScenario, page, request }) => {
    const failedApiRequests = captureFailedApiRequests(page);
    const suffix = e2eScenario.run.slice(-28);
    const firstViewName = `E2E First View ${suffix}`;
    const secondViewName = `E2E Second View ${suffix}`;
    const firstViewSlug = savedViewSlug(firstViewName);
    const secondViewSlug = savedViewSlug(secondViewName);
    const savedViewsPath = `/api/v2/organizations/${e2eScenario.organizationId}/saved-views/events`;
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const filterDefinitions = (time: string) =>
        JSON.stringify([
            { term: 'date', type: 'date', value: `[now-${time} TO now]` },
            { type: 'project', value: [] },
            { type: 'status', value: ['open', 'regressed'] }
        ]);

    const firstSavedViewResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            columns: {
                date: { position: 1, visible: true },
                summary: { position: 0, visible: true }
            },
            filter: '(status:open OR status:regressed)',
            filter_definitions: filterDefinitions('15m'),
            name: firstViewName,
            organization_id: e2eScenario.organizationId,
            show_chart: true,
            show_stats: true,
            slug: firstViewSlug,
            time: '[now-15m TO now]',
            view_type: 'events'
        },
        headers: authorizationHeaders
    });
    expect(firstSavedViewResponse.status()).toBe(201);
    const firstSavedView = (await firstSavedViewResponse.json()) as { id: string };

    const secondSavedViewResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            filter: '(status:open OR status:regressed)',
            filter_definitions: filterDefinitions('1d'),
            name: secondViewName,
            organization_id: e2eScenario.organizationId,
            show_chart: true,
            show_stats: true,
            slug: secondViewSlug,
            time: '[now-1d TO now]',
            view_type: 'events'
        },
        headers: authorizationHeaders
    });
    expect(secondSavedViewResponse.status()).toBe(201);

    await expect
        .poll(
            async () => {
                const response = await request.get(savedViewsPath, { headers: authorizationHeaders });
                if (!response.ok()) {
                    return false;
                }

                const viewNames = ((await response.json()) as { name: string }[]).map((view) => view.name);
                return viewNames.includes(firstViewName) && viewNames.includes(secondViewName);
            },
            { timeout: 30_000 }
        )
        .toBe(true);

    await page.goto(`/next/event/${firstViewSlug}`);
    const dateFilter = page.getByRole('button', { name: /^Date/ }).filter({ visible: true }).first();
    await dateFilter.click();
    await page.getByRole('button', { name: 'Last 90 days' }).click();
    await expect(page).toHaveURL(/[?&]time=90d(?:&|$)/);

    await openViewMenu(page);
    await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();
    const columnDialog = page.getByRole('dialog', { name: 'Column Picker' });
    const summaryWrap = columnDialog.getByRole('checkbox', { name: 'Summary wrap text' });
    await expect(summaryWrap).not.toBeChecked();
    await summaryWrap.click();
    const moveUserUp = columnDialog.getByRole('button', { name: 'Move User up' });
    await moveUserUp.click();
    await moveUserUp.click();
    await columnDialog.getByRole('button', { name: 'Done' }).click();
    await expectColumnBefore(page, 'User', 'Summary');

    const firstViewLink = page.getByRole('link', { exact: true, name: firstViewName }).first();
    const secondViewLink = page.getByRole('link', { exact: true, name: secondViewName }).first();

    await secondViewLink.click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(secondViewSlug)}(?:[?#]|$)`));
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 24 hours/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();

    await dateFilter.click();
    await page.getByRole('button', { name: 'Last 90 days' }).click();
    await expect(page).toHaveURL(/[?&]time=90d(?:&|$)/);
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expectColumnBefore(page, 'Summary', 'User');

    await firstViewLink.click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(firstViewSlug)}(?:[?#]|$)`));
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expectColumnBefore(page, 'User', 'Summary');
    await openViewMenu(page);
    await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();
    await expect(summaryWrap).toBeChecked();
    await columnDialog.getByRole('button', { name: 'Done' }).click();

    const refreshedSavedViews = page.waitForResponse(
        (response) => response.request().method() === 'GET' && new URL(response.url()).pathname === savedViewsPath && response.ok()
    );
    const serverUpdateResponse = await request.patch(`/api/v2/saved-views/${firstSavedView.id}`, {
        data: { show_chart: false },
        headers: authorizationHeaders
    });
    expect(serverUpdateResponse.status()).toBe(200);
    await refreshedSavedViews;

    await secondViewLink.click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(secondViewSlug)}(?:[?#]|$)`));
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await openViewMenu(page);
    await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();
    await expect(summaryWrap).not.toBeChecked();
    await columnDialog.getByRole('button', { name: 'Done' }).click();

    await page.reload();
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await firstViewLink.click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(firstViewSlug)}(?:[?#]|$)`));
    await expect(page).toHaveURL(/[?&]time=90d(?:&|$)/);
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expectColumnBefore(page, 'User', 'Summary');
    await openViewMenu(page);
    await expect(page.getByRole('menuitemcheckbox', { name: 'Chart' })).not.toBeChecked();
    await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();
    await expect(summaryWrap).toBeChecked();
    await columnDialog.getByRole('button', { name: 'Done' }).click();

    await openViewMenu(page);
    await page.getByRole('menuitem', { name: 'Reset to Saved' }).click();
    await expect(page).not.toHaveURL(/[?&]time=90d(?:&|$)/);
    await secondViewLink.click();
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await firstViewLink.click();
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 15 minutes/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
    await expectColumnBefore(page, 'Summary', 'User');
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
