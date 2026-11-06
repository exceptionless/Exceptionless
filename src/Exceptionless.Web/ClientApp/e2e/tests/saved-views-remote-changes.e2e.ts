import type { APIRequestContext, Page } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';

const SAVED_VIEW_NOT_FOUND_HEADING = "We couldn't find that page";

// The navbar search is part of the app layout whether or not Exie is enabled. The full-page error has no layout.
function appLayout(page: Page) {
    return page.getByRole('button', { exact: true, name: 'Search Exceptionless' });
}

async function createStackView(request: APIRequestContext, organizationId: string, token: string, name: string, environment = 'production') {
    const response = await request.post(`/api/v2/organizations/${organizationId}/saved-views`, {
        data: { filter: viewFilter(environment), filter_definitions: viewDefinitions(environment), name, organization_id: organizationId, view_type: 'stacks' },
        headers: { Authorization: `Bearer ${token}` }
    });
    expect(response.ok()).toBe(true);
    return (await response.json()) as { id: string; slug: string };
}

// Follows a link the way the app does, without a full page load, as an answer from Exie would.
async function followClientLink(page: Page, href: string): Promise<void> {
    await page.evaluate((target) => {
        const link = document.createElement('a');
        link.href = target;
        link.textContent = 'test link';
        document.body.append(link);
        link.click();
        link.remove();
    }, href);
}

function viewDefinitions(environment: string) {
    return JSON.stringify([
        { type: 'project', value: [] },
        { hidden: true, type: 'status', value: ['open', 'regressed'] },
        { type: 'environment', value: [environment] }
    ]);
}

function viewFilter(environment: string): string {
    return `(status:open OR status:regressed) environment:${environment}`;
}

test('an open saved view changed elsewhere shows the new filters without unsaved changes', async ({ e2eScenario, page, request }) => {
    // Arrange
    const token = e2eScenario.userToken;
    const viewName = `E2E Changed View ${e2eScenario.run.slice(-28)}`;
    const savedView = await createStackView(request, e2eScenario.organizationId, token, viewName);
    await page.goto(`/stack/${savedView.slug}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByRole('button', { name: /production/ }).first()).toBeVisible({ timeout: 30_000 });

    // Act
    const updated = await request.patch(`/api/v2/saved-views/${savedView.id}`, {
        data: { filter: viewFilter('staging'), filter_definitions: viewDefinitions('staging'), time: '[now-30d TO now]' },
        headers: { Authorization: `Bearer ${token}` }
    });
    expect(updated.ok()).toBe(true);

    // Assert
    await expect(page.getByRole('button', { name: /staging/ }).first()).toBeVisible({ timeout: 30_000 });
    await page.getByRole('button', { exact: true, name: 'View' }).click();
    await expect(page.getByRole('menuitem', { name: 'Reset to Saved' })).toBeDisabled();
    await expect(page.getByLabel('Unsaved view changes')).toHaveCount(0);
});

test('a link to a view created moments ago opens it', async ({ e2eScenario, page, request }) => {
    // Arrange
    const viewName = `E2E Fresh View ${e2eScenario.run.slice(-28)}`;
    await page.goto('/stack');
    await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
    await expect(page.getByRole('button', { exact: true, name: 'View' })).toBeVisible({ timeout: 30_000 });

    // Act
    const savedView = await createStackView(request, e2eScenario.organizationId, e2eScenario.userToken, viewName);
    await followClientLink(page, `/stack/${savedView.slug}`);

    // Assert
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
});

test('going back to a view deleted elsewhere keeps the app open', async ({ e2eScenario, page, request }) => {
    // Arrange
    const token = e2eScenario.userToken;
    const viewName = `E2E Back View ${e2eScenario.run.slice(-28)}`;
    const savedView = await createStackView(request, e2eScenario.organizationId, token, viewName);
    await page.goto('/stack');
    await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
    await followClientLink(page, `/stack/${savedView.slug}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
    const deleted = await request.delete(`/api/v2/saved-views/${savedView.id}`, { headers: { Authorization: `Bearer ${token}` } });
    expect(deleted.ok()).toBe(true);
    await expect(page).toHaveURL(/\/stack(?:[?#]|$)/, { timeout: 30_000 });

    // Act
    await page.goBack();

    // Assert
    await expect(page).toHaveURL(/\/stack(?:[?#]|$)/);
    await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });
    await expect(appLayout(page)).toBeVisible();
});

test('an old link to a deleted view keeps the app open', async ({ e2eScenario, page }) => {
    // Arrange
    await page.goto('/stack');
    await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible({ timeout: 30_000 });

    // Act
    await followClientLink(
        page,
        `/stack/no-such-view-${e2eScenario.run
            .replace(/[^a-zA-Z0-9]/g, '')
            .slice(-12)
            .toLowerCase()}`
    );

    // Assert
    await expect(page.getByRole('heading', { name: SAVED_VIEW_NOT_FOUND_HEADING })).toBeVisible({ timeout: 30_000 });
    await expect(appLayout(page)).toBeVisible();
});

test('a view deleted while disconnected keeps the app open after reconnecting', async ({ e2eScenario, page, request }) => {
    // Arrange
    const token = e2eScenario.userToken;
    const viewName = `E2E Offline View ${e2eScenario.run.slice(-28)}`;
    const savedView = await createStackView(request, e2eScenario.organizationId, token, viewName);
    let dropServerMessages = false;
    let closeSocket: () => Promise<void> = async () => {};
    await page.routeWebSocket(/\/push/, (socket) => {
        const server = socket.connectToServer();
        socket.onMessage((message) => server.send(message));
        server.onMessage((message) => {
            if (!dropServerMessages) {
                socket.send(message);
            }
        });
        closeSocket = () => socket.close();
    });
    await page.goto(`/stack/${savedView.slug}`);
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });

    // Act
    dropServerMessages = true;
    const deleted = await request.delete(`/api/v2/saved-views/${savedView.id}`, { headers: { Authorization: `Bearer ${token}` } });
    expect(deleted.ok()).toBe(true);
    dropServerMessages = false;
    await closeSocket();

    // Assert
    await expect(page.getByRole('heading', { name: SAVED_VIEW_NOT_FOUND_HEADING })).toBeVisible({ timeout: 30_000 });
    await expect(appLayout(page)).toBeVisible();
});
