import type { Locator, Page, Request } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

test('home navigation honors personal and organization saved views and survives deletion', async ({ e2eApi, e2eScenario, page, request }) => {
    const failedApiRequests = captureFailedApiRequests(page);
    const savedViewListLimits: string[] = [];
    page.on('request', (request) => {
        const url = new URL(request.url());
        if (url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}/saved-views`) {
            savedViewListLimits.push(url.searchParams.get('limit') ?? '');
        }
    });
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const viewName = `E2E Home ${journey.run.slice(-36)}`;
    const viewSlug = savedViewSlug(viewName);

    await test.step('fall back to the first Stacks saved view when no default is configured', async () => {
        await page.goto('/next/');
        await expect(page).toHaveURL(/\/next\/stack\/all(?:[?#]|$)/);
        await expect(page.getByRole('heading', { name: 'All' })).toBeVisible({ timeout: 30_000 });
        await expect.poll(() => savedViewListLimits).toContain('100');
    });

    await test.step('prefer the personal saved view', async () => {
        await journey.submitRepresentativeEvent();
        await page.goto(`/next/event?reference=${encodeURIComponent(journey.referenceId)}&time=all`);
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Save As...' }).click();
        const dialog = page.getByRole('dialog', { name: 'Save View' });
        await dialog.getByLabel('Name', { exact: true }).fill(viewName);
        await dialog.getByRole('button', { name: 'Save' }).click();
        await expect(dialog).toBeHidden({ timeout: 30_000 });
        await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Set as my home view' }).click();
        await expect(page.getByText(`"${viewName}" is now your home view.`)).toBeVisible();

        await expect
            .poll(
                async () => {
                    const response = await request.get(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views/events`, {
                        headers: { Authorization: `Bearer ${e2eScenario.userToken}` }
                    });
                    const savedViews = response.ok() ? ((await response.json()) as { name: string }[]) : [];
                    return savedViews.some((savedView) => savedView.name === viewName);
                },
                { timeout: 30_000 }
            )
            .toBe(true);

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
            async ({ organizationId, token, viewName }) => {
                const headers = { Authorization: `Bearer ${token}` };
                const savedViewsResponse = await fetch(`/api/v2/organizations/${organizationId}/saved-views/events`, { headers });
                const savedViews = await savedViewsResponse.json();
                const savedView = savedViews.find((view: { name: string }) => view.name === viewName);
                const response = await fetch(`/api/v2/saved-views/${savedView.id}`, {
                    headers,
                    method: 'DELETE'
                });
                return response.status;
            },
            { organizationId: e2eScenario.organizationId, token: e2eScenario.userToken, viewName }
        );
        expect(deletion).toBe(202);

        await page.goto('/next/');
        await expect(page).toHaveURL(/\/next\/stack\/all(?:[?#]|$)/);
    });

    expect(failedApiRequests).toEqual([]);
});

test('saved view navigation order is personal, persistent, and resettable', async ({ e2eScenario, page, request }) => {
    const failedApiRequests = captureFailedApiRequests(page);
    const suffix = e2eScenario.run.slice(-24);
    const sharedViewName = `AAA Shared ${suffix}`;
    const privateViewName = `ZZZ Private ${suffix}`;
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const savedViewsPath = `/api/v2/organizations/${e2eScenario.organizationId}/saved-views`;

    for (const [name, isPrivate] of [
        [sharedViewName, false],
        [privateViewName, true]
    ] as const) {
        const response = await request.post(savedViewsPath, {
            data: {
                filter: 'type:error',
                is_private: isPrivate,
                name,
                organization_id: e2eScenario.organizationId,
                slug: savedViewSlug(name),
                view_type: 'events'
            },
            headers: authorizationHeaders
        });
        expect(response.status()).toBe(201);
    }

    await expect
        .poll(
            async () => {
                const response = await request.get(`${savedViewsPath}/events`, { headers: authorizationHeaders });
                const names = response.ok() ? ((await response.json()) as { name: string }[]).map((savedView) => savedView.name) : [];
                return names.includes(sharedViewName) && names.includes(privateViewName);
            },
            { timeout: 30_000 }
        )
        .toBe(true);

    await page.goto('/next/event');
    const sidebar = page.locator('[data-sidebar="sidebar"]');
    const sharedViewLink = sidebar.getByRole('link', { exact: true, name: sharedViewName });
    const privateViewLink = sidebar.getByRole('link', { exact: true, name: privateViewName });
    await expect(sharedViewLink).toBeVisible({ timeout: 30_000 });
    await expect(privateViewLink).toBeVisible();
    await expectSavedViewBefore(sharedViewLink, privateViewLink);

    await sharedViewLink.click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(savedViewSlug(sharedViewName))}(?:[?#]|$)`));
    await page.goto('/next/event');
    await expect(privateViewLink).toBeVisible({ timeout: 30_000 });

    await expect(privateViewLink).toHaveAttribute('draggable', 'true');
    const privateViewBox = await privateViewLink.boundingBox();
    const sharedViewBox = await sharedViewLink.boundingBox();
    if (!privateViewBox || !sharedViewBox) {
        throw new Error('Saved view links must have visible bounding boxes');
    }

    await page.mouse.move(privateViewBox.x + privateViewBox.width / 2, privateViewBox.y + privateViewBox.height / 2);
    await page.mouse.down();
    await page.mouse.move(privateViewBox.x + privateViewBox.width / 2, privateViewBox.y - 8, { steps: 4 });
    await page.mouse.move(sharedViewBox.x + sharedViewBox.width / 2, sharedViewBox.y + sharedViewBox.height / 2, { steps: 12 });
    await expectSavedViewBefore(sharedViewLink, privateViewLink);
    await page.mouse.up();
    await expect(page.getByText('Events view order saved.')).toBeVisible();
    await expectSavedViewBefore(privateViewLink, sharedViewLink);

    await page.reload();
    await expect(privateViewLink).toBeVisible({ timeout: 30_000 });
    await expectSavedViewBefore(privateViewLink, sharedViewLink);

    const currentUserResponse = await request.get('/api/v2/users/me', { headers: authorizationHeaders });
    expect(currentUserResponse.ok()).toBe(true);
    const currentUser = (await currentUserResponse.json()) as {
        saved_view_orders: { organization_id: string; saved_view_ids: string[]; view_type: string }[];
    };
    expect(
        currentUser.saved_view_orders.find((preference) => preference.organization_id === e2eScenario.organizationId && preference.view_type === 'events')
            ?.saved_view_ids
    ).toBeDefined();

    await page.getByRole('button', { name: 'Reorder Events views' }).click();
    const dialog = page.getByRole('dialog', { name: 'Reorder Events Views' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(privateViewName, { exact: true }).locator('..').getByText('Private', { exact: true })).toBeVisible();
    await expect(dialog.getByText(sharedViewName, { exact: true }).locator('..').getByText('Shared', { exact: true })).toBeVisible();
    await dialog.getByRole('button', { name: 'Reset to alphabetical' }).click();
    await expect(dialog).toBeHidden();
    await expectSavedViewBefore(sharedViewLink, privateViewLink);
    await page.reload();
    await expect(sharedViewLink).toBeVisible({ timeout: 30_000 });
    await expectSavedViewBefore(sharedViewLink, privateViewLink);

    const resetCurrentUserResponse = await request.get('/api/v2/users/me', { headers: authorizationHeaders });
    const resetCurrentUser = (await resetCurrentUserResponse.json()) as {
        saved_view_orders: { organization_id: string; saved_view_ids: string[]; view_type: string }[];
    };
    expect(
        resetCurrentUser.saved_view_orders.find((preference) => preference.organization_id === e2eScenario.organizationId && preference.view_type === 'events')
    ).toBeUndefined();

    expect(failedApiRequests).toEqual([]);
});

test('events saved view can be saved, renamed, loaded, and deleted', async ({ e2eApi, e2eScenario, page, request }) => {
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

    await test.step('updating a URL override clears the hidden session-local draft', async () => {
        await page.goto(`/next/event/${viewSlug}?time=90d`);
        const dateFilter = page.getByRole('button', { name: /^Date/ }).filter({ visible: true }).first();
        await dateFilter.click();
        await page.getByRole('button', { name: 'Last 30 days' }).click();
        await expect(page).toHaveURL(/[?&]time=30d(?:&|$)/);
        await expect(page.getByLabel('Unsaved view changes')).toBeVisible();

        await page.goto(`/next/event/${viewSlug}?time=1d`);
        await expect(
            page
                .getByRole('button', { name: /Date\s+Last 24 hours/ })
                .filter({ visible: true })
                .first()
        ).toBeVisible();
        await openViewMenu(page);
        await page.getByRole('menuitem', { exact: true, name: 'Save' }).click();
        await expect(page.getByText(`View "${renamedViewName}" saved.`)).toBeVisible();
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
        await expect
            .poll(async () => {
                const response = await request.get(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views/events`, {
                    headers: { Authorization: `Bearer ${e2eScenario.userToken}` }
                });
                const view = ((await response.json()) as { slug: string; time?: string }[]).find((candidate) => candidate.slug === viewSlug);
                return view?.time;
            })
            .toContain('now-1d');

        await page.goto(`/next/event/${viewSlug}`);
        await expect(
            page
                .getByRole('button', { name: /Date\s+Last 24 hours/ })
                .filter({ visible: true })
                .first()
        ).toBeVisible();
        await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
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
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
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
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await page.evaluate(
        ({ firstHref, secondHref }) => {
            const navigate = (href: string) => {
                const link = document.createElement('a');
                link.href = href;
                document.body.append(link);
                link.click();
            };

            navigate(secondHref);
            setTimeout(() => navigate(firstHref), 0);
        },
        {
            firstHref: `/next/event/${firstViewSlug}`,
            secondHref: `/next/event/${secondViewSlug}`
        }
    );
    await page.waitForTimeout(100);
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expectColumnBefore(page, 'User', 'Summary');

    const coldLoadEventTimes: string[] = [];
    const captureColdLoadEventTime = (request: Request) => {
        const url = new URL(request.url());
        if (url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}/events`) {
            coldLoadEventTimes.push(url.searchParams.get('time') ?? '');
        }
    };
    page.on('request', captureColdLoadEventTime);
    await page.goto(`/next/event/${firstViewSlug}?project=${e2eScenario.projectId}&sort=type&status=open`);
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`[?&]project=${escapeRegExp(e2eScenario.projectId)}(?:&|$)`));
    await expect(page).toHaveURL(/[?&]sort=type(?:&|$)/);
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect.poll(() => coldLoadEventTimes.length).toBeGreaterThan(0);
    page.off('request', captureColdLoadEventTime);
    expect(coldLoadEventTimes.every((time) => time.includes('now-90d'))).toBe(true);
    await page.reload();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await navigateClientSide(page, `/next/event/${firstViewSlug}?project=${e2eScenario.projectId}&sort=type&status=regressed&time=90d`);
    await expect(page).toHaveURL(/[?&]status=regressed(?:&|$)/);
    await secondViewLink.click();
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();

    await firstViewLink.click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(firstViewSlug)}(?:[?#]|$)`));
    await expect(page).not.toHaveURL(/[?&]status=/);
    await expect(page).not.toHaveURL(/[?&]project=/);
    await expect(page).not.toHaveURL(/[?&]sort=/);
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expectColumnBefore(page, 'User', 'Summary');

    await page.goto(`/next/event/${firstViewSlug}?time=15m`);
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 15 minutes/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await secondViewLink.click();
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await firstViewLink.click();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page).not.toHaveURL(/[?&]status=/);

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

    await navigateClientSide(page, '/next/stack');
    await expect(page).toHaveURL(/\/next\/stack(?:[?#]|$)/);
    await navigateClientSide(page, `/next/event/${firstViewSlug}`);
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await page.goBack();
    await expect(page).toHaveURL(/\/next\/stack(?:[?#]|$)/);
    await navigateClientSide(page, `/next/event/${firstViewSlug}`);
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();

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

test('save completion does not overwrite a newly active saved view', async ({ e2eScenario, page, request }) => {
    const suffix = e2eScenario.run.slice(-28);
    const firstViewName = `E2E Save Race A ${suffix}`;
    const secondViewName = `E2E Save Race B ${suffix}`;
    const firstViewSlug = savedViewSlug(firstViewName);
    const secondViewSlug = savedViewSlug(secondViewName);
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const createView = (name: string, slug: string, time: string) =>
        request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
            data: {
                filter: '(status:open OR status:regressed)',
                filter_definitions: JSON.stringify([
                    { term: 'date', type: 'date', value: `[now-${time} TO now]` },
                    { type: 'project', value: [] },
                    { type: 'status', value: ['open', 'regressed'] }
                ]),
                name,
                organization_id: e2eScenario.organizationId,
                slug,
                time: `[now-${time} TO now]`,
                view_type: 'events'
            },
            headers: authorizationHeaders
        });

    const firstViewResponse = await createView(firstViewName, firstViewSlug, '15m');
    expect(firstViewResponse.status()).toBe(201);
    const firstView = (await firstViewResponse.json()) as { id: string };
    const secondViewResponse = await createView(secondViewName, secondViewSlug, '1d');
    expect(secondViewResponse.status()).toBe(201);
    const savedViewsPath = `/api/v2/organizations/${e2eScenario.organizationId}/saved-views/events`;
    await expect
        .poll(async () => {
            const response = await request.get(savedViewsPath, { headers: authorizationHeaders });
            const names = response.ok() ? ((await response.json()) as { name: string }[]).map((view) => view.name) : [];
            return names.includes(firstViewName) && names.includes(secondViewName);
        })
        .toBe(true);

    await page.goto(`/next/event/${firstViewSlug}`);
    await expect(page.getByRole('heading', { name: firstViewName })).toBeVisible();
    await page.getByRole('button', { name: /^Date/ }).filter({ visible: true }).first().click();
    await page.getByRole('button', { name: 'Last 90 days' }).click();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();

    let releaseSave!: () => void;
    let markSaveStarted!: () => void;
    const saveStarted = new Promise<void>((resolve) => {
        markSaveStarted = resolve;
    });
    const saveRelease = new Promise<void>((resolve) => {
        releaseSave = resolve;
    });
    await page.route(`**/api/v2/saved-views/${firstView.id}`, async (route) => {
        if (route.request().method() !== 'PATCH') {
            await route.continue();
            return;
        }

        markSaveStarted();
        await saveRelease;
        await route.continue();
    });

    await openViewMenu(page);
    await page.getByRole('menuitem', { exact: true, name: 'Save' }).click();
    await saveStarted;
    const secondViewRequestTimes: string[] = [];
    page.on('request', (eventRequest) => {
        const url = new URL(eventRequest.url());
        if (url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}/events`) {
            secondViewRequestTimes.push(url.searchParams.get('time') ?? '');
        }
    });
    await page.getByRole('link', { exact: true, name: secondViewName }).first().click();
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await expect.poll(() => secondViewRequestTimes.some((time) => time.includes('now-1d'))).toBe(true);
    await page.getByRole('button', { name: /^Date/ }).filter({ visible: true }).first().click();
    await page.getByRole('button', { name: 'Last 7 days' }).click();
    const saveCompleted = page.waitForResponse(
        (response) => response.request().method() === 'PATCH' && new URL(response.url()).pathname === `/api/v2/saved-views/${firstView.id}`
    );
    releaseSave();
    await saveCompleted;
    await expect.poll(() => secondViewRequestTimes.some((time) => time.includes('now-7d'))).toBe(true);
    await page.reload();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 7 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
});

test('stream switches to a session-local saved view draft without mixing in-flight results', async ({ e2eApi, e2eScenario, page, request }) => {
    const failedApiRequests = captureFailedApiRequests(page);
    const suffix = e2eScenario.run.slice(-28);
    const viewName = `E2E Stream View ${suffix}`;
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const draftExpression = `error.type:Local${suffix}`;
    const remoteExpression = `error.type:Remote${suffix}`;
    const filterDefinitions = JSON.stringify([
        { type: 'project', value: [] },
        { type: 'status', value: ['open', 'regressed'] }
    ]);

    const savedViewResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            filter: '(status:open OR status:regressed)',
            filter_definitions: filterDefinitions,
            name: viewName,
            organization_id: e2eScenario.organizationId,
            view_type: 'stream'
        },
        headers: authorizationHeaders
    });
    expect(savedViewResponse.status()).toBe(201);
    const savedView = (await savedViewResponse.json()) as { id: string };
    const currentUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
    expect(currentUser).toBeDefined();

    const savedViewsPath = `/api/v2/organizations/${e2eScenario.organizationId}/saved-views/stream`;
    await expect
        .poll(
            async () => {
                const response = await request.get(savedViewsPath, { headers: authorizationHeaders });
                return response.ok() && ((await response.json()) as { id: string }[]).some((view) => view.id === savedView.id);
            },
            { timeout: 30_000 }
        )
        .toBe(true);

    await page.addInitScript(
        ({ draft, key }) => {
            window.sessionStorage.setItem(key, JSON.stringify(draft));
        },
        {
            draft: {
                filterChanges: {
                    duplicateKeys: ['keyword'],
                    removedKeys: [],
                    sourceDefinitions: filterDefinitions,
                    upsertDefinitions: JSON.stringify([{ type: 'keyword', value: draftExpression }])
                },
                version: 1
            },
            key: `exceptionless:saved-view-draft:v1:${currentUser!.id}:${e2eScenario.organizationId}:${savedView.id}`
        }
    );

    const sourceMessage = `Source stream result ${suffix}`;
    let releaseSourceRequest!: () => void;
    const sourceRequestRelease = new Promise<void>((resolve) => {
        releaseSourceRequest = resolve;
    });
    let sourceRequestCompleted = false;
    let sourceRequestStarted = false;
    const streamRequestFilters: string[] = [];
    await page.route(`**/api/v2/organizations/${e2eScenario.organizationId}/events*`, async (route) => {
        const filter = new URL(route.request().url()).searchParams.get('filter') ?? '';
        streamRequestFilters.push(filter);
        if (filter.includes(draftExpression)) {
            await route.fulfill({ body: '[]', contentType: 'application/json', status: 200 });
            return;
        }

        sourceRequestStarted = true;
        await sourceRequestRelease;
        await route.fulfill({
            body: JSON.stringify([
                {
                    data: { Message: sourceMessage },
                    date: new Date().toISOString(),
                    id: '000000000000000000000001',
                    project_id: e2eScenario.projectId,
                    tags: [],
                    template_key: 'event-simple-summary'
                }
            ]),
            contentType: 'application/json',
            status: 200
        });
        sourceRequestCompleted = true;
    });

    await page.goto('/next/stream');
    await expect.poll(() => sourceRequestStarted).toBe(true);
    await page.getByRole('button', { name: 'Pause streaming updates' }).click();
    await navigateClientSide(page, `/next/stream?saved=${savedView.id}`);
    releaseSourceRequest();
    await expect.poll(() => sourceRequestCompleted).toBe(true);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect(getVisibleText(page, sourceMessage)).toBeHidden();
    await page.getByRole('button', { name: 'Resume streaming updates' }).click();
    await expect.poll(() => streamRequestFilters.some((filter) => filter.includes(draftExpression))).toBe(true);
    await expect(getVisibleText(page, sourceMessage)).toBeHidden();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expect
        .poll(() =>
            page.evaluate(() => {
                const state = window.history.state as { 'sveltekit:states'?: { __exceptionlessSavedViewDraftFilter?: unknown } };
                return state['sveltekit:states']?.__exceptionlessSavedViewDraftFilter;
            })
        )
        .toEqual({ filter: new URL(page.url()).searchParams.get('filter'), viewId: savedView.id });

    const updatedFilterDefinitions = JSON.stringify([
        { type: 'keyword', value: remoteExpression },
        { type: 'project', value: [] },
        { type: 'status', value: ['fixed'] }
    ]);
    const serverUpdateResponse = await request.patch(`/api/v2/saved-views/${savedView.id}`, {
        data: {
            filter: `status:fixed AND ${remoteExpression}`,
            filter_definitions: updatedFilterDefinitions
        },
        headers: authorizationHeaders
    });
    expect(serverUpdateResponse.status()).toBe(200);
    await expect
        .poll(
            async () => {
                const response = await request.get(savedViewsPath, { headers: authorizationHeaders });
                const views = response.ok() ? ((await response.json()) as { filter_definitions: string; id: string }[]) : [];
                return views.find((view) => view.id === savedView.id)?.filter_definitions;
            },
            { timeout: 30_000 }
        )
        .toBe(updatedFilterDefinitions);

    streamRequestFilters.length = 0;
    await page.reload();
    await expect
        .poll(() =>
            page.evaluate(() => {
                const state = window.history.state as { 'sveltekit:states'?: { __exceptionlessSavedViewDraftFilter?: unknown } };
                return state['sveltekit:states']?.__exceptionlessSavedViewDraftFilter;
            })
        )
        .toEqual({ filter: new URL(page.url()).searchParams.get('filter'), viewId: savedView.id });
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect.poll(() => streamRequestFilters.join('\n')).toContain(remoteExpression);
    const restoredFilter = streamRequestFilters.findLast((filter) => filter.includes(draftExpression) && filter.includes(remoteExpression))!;
    expect(restoredFilter.split(draftExpression)).toHaveLength(2);
    expect(restoredFilter).toContain('fixed');
    expect(restoredFilter).toContain(remoteExpression);
    expect(restoredFilter).not.toContain('open');
    expect(restoredFilter).not.toContain('regressed');
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();

    streamRequestFilters.length = 0;
    await page.goto(`/next/stream?saved=${savedView.id}&filter=${encodeURIComponent(draftExpression)}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Status Fixed' })).toHaveCount(0);
    await expect.poll(() => streamRequestFilters.some((filter) => filter.includes(draftExpression))).toBe(true);
    const explicitFilter = streamRequestFilters.findLast((filter) => filter.includes(draftExpression))!;
    expect(explicitFilter).not.toContain('fixed');
    expect(explicitFilter).not.toContain(remoteExpression);

    streamRequestFilters.length = 0;
    await page.goto(`/next/stream?saved=${savedView.id}&filter=`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Status Fixed' })).toHaveCount(0);
    await expect.poll(() => streamRequestFilters.at(-1)).toBe('');
    expect(failedApiRequests).toEqual([]);
});

test('stream loads default results when its saved-view lookup fails', async ({ e2eScenario, page }) => {
    await page.route(`**/api/v2/organizations/${e2eScenario.organizationId}/saved-views/stream*`, async (route) => {
        await route.fulfill({
            body: JSON.stringify({ detail: 'Simulated saved-view failure', status: 500, title: 'Internal Server Error' }),
            contentType: 'application/problem+json',
            status: 500
        });
    });
    const eventRequests: string[] = [];
    page.on('request', (request) => {
        const url = new URL(request.url());
        if (url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}/events`) {
            eventRequests.push(request.url());
        }
    });

    await page.goto('/next/stream?saved=unavailable-view');
    await expect(page.getByRole('heading', { name: 'Event Stream' })).toBeVisible();
    await expect.poll(() => eventRequests.length, { timeout: 30_000 }).toBeGreaterThan(0);
});

test('reset clears a session-local draft after delayed current-user identity resolves', async ({ e2eApi, e2eScenario, page, request }) => {
    const suffix = e2eScenario.run.slice(-28);
    const viewName = `E2E Delayed Reset ${suffix}`;
    const viewSlug = savedViewSlug(viewName);
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const savedViewResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            filter: '(status:open OR status:regressed)',
            filter_definitions: JSON.stringify([
                { term: 'date', type: 'date', value: '[now-15m TO now]' },
                { type: 'project', value: [] },
                { type: 'status', value: ['open', 'regressed'] }
            ]),
            name: viewName,
            organization_id: e2eScenario.organizationId,
            slug: viewSlug,
            time: '[now-15m TO now]',
            view_type: 'events'
        },
        headers: authorizationHeaders
    });
    expect(savedViewResponse.status()).toBe(201);
    const savedView = (await savedViewResponse.json()) as { id: string };
    const currentUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
    expect(currentUser).toBeDefined();

    await page.addInitScript(({ draft, key }) => window.sessionStorage.setItem(key, JSON.stringify(draft)), {
        draft: {
            filterChanges: {
                baselineDefinitions: '[{"term":"date","type":"date","value":"[now-15m TO now]"}]',
                removedDefinitions: '[{"term":"date","type":"date","value":"[now-15m TO now]"}]',
                removedKeys: [],
                upsertDefinitions: '[{"term":"date","type":"date","value":"[now-90d TO now]"}]'
            },
            version: 1
        },
        key: `exceptionless:saved-view-draft:v1:${currentUser!.id}:${e2eScenario.organizationId}:${savedView.id}`
    });

    let releaseCurrentUser!: () => void;
    const currentUserRelease = new Promise<void>((resolve) => {
        releaseCurrentUser = resolve;
    });
    await page.route('**/api/v2/users/me', async (route) => {
        await currentUserRelease;
        await route.continue();
    });

    await page.goto(`/next/event/${viewSlug}?time=1d`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await openViewMenu(page);
    const resetMenuItem = page.getByRole('menuitem', { name: 'Reset to Saved' });
    await expect(resetMenuItem).toBeDisabled();
    releaseCurrentUser();
    await expect(resetMenuItem).toBeEnabled();
    await resetMenuItem.click();
    await expect(page).not.toHaveURL(/[?&]time=/);

    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 15 minutes/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
});

test('filter edits made before current-user identity resolves become session-local drafts', async ({ e2eApi, e2eScenario, page, request }) => {
    const suffix = e2eScenario.run.slice(-28);
    const viewName = `E2E Delayed Edit ${suffix}`;
    const viewSlug = savedViewSlug(viewName);
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const savedViewResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            filter: '(status:open OR status:regressed)',
            filter_definitions: JSON.stringify([
                { term: 'date', type: 'date', value: '[now-15m TO now]' },
                { type: 'project', value: [] },
                { type: 'status', value: ['open', 'regressed'] }
            ]),
            name: viewName,
            organization_id: e2eScenario.organizationId,
            slug: viewSlug,
            time: '[now-15m TO now]',
            view_type: 'events'
        },
        headers: authorizationHeaders
    });
    expect(savedViewResponse.status()).toBe(201);
    const savedView = (await savedViewResponse.json()) as { id: string };
    const currentUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
    expect(currentUser).toBeDefined();
    const draftKey = `exceptionless:saved-view-draft:v1:${currentUser!.id}:${e2eScenario.organizationId}:${savedView.id}`;
    await page.addInitScript(
        ({ key, marker }) => {
            if (window.sessionStorage.getItem(marker)) {
                return;
            }

            window.sessionStorage.setItem(key, JSON.stringify({ showChart: false, version: 1 }));
            window.sessionStorage.setItem(marker, 'true');
        },
        { key: draftKey, marker: `${draftKey}:seeded` }
    );

    let releaseCurrentUser!: () => void;
    const currentUserRelease = new Promise<void>((resolve) => {
        releaseCurrentUser = resolve;
    });
    await page.route('**/api/v2/users/me', async (route) => {
        await currentUserRelease;
        await route.continue();
    });

    await page.goto(`/next/event/${viewSlug}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await openViewMenu(page);
    const chartMenuItem = page.getByRole('menuitemcheckbox', { name: 'Chart' });
    await expect(chartMenuItem).toBeChecked();
    await chartMenuItem.click();
    await expect(chartMenuItem).not.toBeChecked();
    await chartMenuItem.click();
    await expect(chartMenuItem).toBeChecked();
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: /^Date/ }).filter({ visible: true }).first().click();
    await page.getByRole('button', { name: 'Last 90 days' }).click();
    await expect(page).toHaveURL(/[?&]time=90d(?:&|$)/);

    releaseCurrentUser();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    await expect.poll(() => page.evaluate((key) => window.sessionStorage.getItem(key), draftKey)).not.toBeNull();
    await openViewMenu(page);
    await expect(page.getByRole('menuitemcheckbox', { name: 'Chart' })).toBeChecked();
    await page.keyboard.press('Escape');
    await expect
        .poll(async () => JSON.parse((await page.evaluate((key) => window.sessionStorage.getItem(key), draftKey)) ?? '{}') as { showChart?: boolean })
        .not.toHaveProperty('showChart');
    await expect
        .poll(async () => {
            const draft = JSON.parse((await page.evaluate((key) => window.sessionStorage.getItem(key), draftKey)) ?? '{}') as {
                filterChanges?: { upsertDefinitions?: string };
            };
            return draft.filterChanges?.upsertDefinitions;
        })
        .toContain('now-90d');

    await page.goto(`/next/event/${viewSlug}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect
        .poll(async () => {
            const draft = JSON.parse((await page.evaluate((key) => window.sessionStorage.getItem(key), draftKey)) ?? '{}') as {
                filterChanges?: { upsertDefinitions?: string };
            };
            return draft.filterChanges?.upsertDefinitions;
        })
        .toContain('now-90d');
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
});

test('saved view loads server state and protects drafts when the current-user lookup fails', async ({ e2eApi, e2eScenario, page, request }) => {
    const suffix = e2eScenario.run.slice(-28);
    const viewName = `E2E User Failure ${suffix}`;
    const viewSlug = savedViewSlug(viewName);
    const authorizationHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const filterDefinitions = JSON.stringify([
        { term: 'date', type: 'date', value: '[now-15m TO now]' },
        { type: 'project', value: [] },
        { type: 'status', value: ['open', 'regressed'] }
    ]);

    const savedViewResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
        data: {
            filter: '(status:open OR status:regressed)',
            filter_definitions: filterDefinitions,
            name: viewName,
            organization_id: e2eScenario.organizationId,
            slug: viewSlug,
            time: '[now-15m TO now]',
            view_type: 'events'
        },
        headers: authorizationHeaders
    });
    expect(savedViewResponse.status()).toBe(201);
    const savedView = (await savedViewResponse.json()) as { id: string };
    const currentUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
    expect(currentUser).toBeDefined();
    const draftKey = `exceptionless:saved-view-draft:v1:${currentUser!.id}:${e2eScenario.organizationId}:${savedView.id}`;
    await page.addInitScript(({ draft, key }) => window.sessionStorage.setItem(key, JSON.stringify(draft)), {
        draft: {
            filterChanges: {
                baselineDefinitions: '[{"term":"date","type":"date","value":"[now-15m TO now]"}]',
                removedDefinitions: '[{"term":"date","type":"date","value":"[now-15m TO now]"}]',
                removedKeys: [],
                upsertDefinitions: '[{"term":"date","type":"date","value":"[now-90d TO now]"}]'
            },
            version: 1
        },
        key: draftKey
    });

    const savedViewsPath = `/api/v2/organizations/${e2eScenario.organizationId}/saved-views/events`;
    await expect
        .poll(
            async () => {
                const response = await request.get(savedViewsPath, { headers: authorizationHeaders });
                return response.ok() && ((await response.json()) as { name: string }[]).some((view) => view.name === viewName);
            },
            { timeout: 30_000 }
        )
        .toBe(true);

    await page.route('**/api/v2/users/me', async (route) => {
        await route.fulfill({
            body: JSON.stringify({ detail: 'Simulated current-user failure', status: 500, title: 'Internal Server Error' }),
            contentType: 'application/problem+json',
            status: 500
        });
    });
    const eventRequests: string[] = [];
    page.on('request', (request) => {
        const url = new URL(request.url());
        if (url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}/events`) {
            eventRequests.push(request.url());
        }
    });

    await page.goto(`/next/event/${viewSlug}?time=1d`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect.poll(() => eventRequests.length, { timeout: 30_000 }).toBeGreaterThan(0);
    await openViewMenu(page);
    await expect(page.getByRole('menuitem', { exact: true, name: 'Save' })).toBeDisabled();
    await expect(page.getByRole('menuitem', { name: 'Reset to Saved' })).toBeDisabled();

    await page.unroute('**/api/v2/users/me');
    await page.goto(`/next/event/${viewSlug}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 90 days/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
    await expect(page.getByLabel('Unsaved view changes')).toBeVisible();
    expect(await page.evaluate((key) => window.sessionStorage.getItem(key), draftKey)).not.toBeNull();
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

async function expectSavedViewBefore(first: Locator, second: Locator): Promise<void> {
    await expect
        .poll(async () => {
            const firstBox = await first.boundingBox();
            const secondBox = await second.boundingBox();
            return !!firstBox && !!secondBox && firstBox.y < secondBox.y;
        })
        .toBe(true);
}

async function navigateClientSide(page: Page, href: string): Promise<void> {
    await page.evaluate((target) => {
        const link = document.createElement('a');
        link.href = target;
        document.body.append(link);
        link.click();
    }, href);
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
