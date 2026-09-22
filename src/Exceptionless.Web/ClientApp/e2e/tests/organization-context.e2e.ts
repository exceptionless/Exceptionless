import { expect, test } from '../fixtures/organization-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { getVisibleText } from '../support/page-helpers';
import { createSessionEvent } from '../support/synthetic-event';

test('account menu organization actions target the impersonated organization', async ({ e2eApi, e2eScenario, impersonatedOrganization, page }) => {
    const currentUser = await e2eApi.getCurrentUser(e2eScenario.userToken);
    await page.goto(`/next/organization/${impersonatedOrganization.organizationId}/manage`);
    await expect(page.getByRole('heading', { name: `${impersonatedOrganization.organizationName} Settings` })).toBeVisible();
    await page.getByRole('button').filter({ hasText: currentUser!.email_address! }).click();
    await expect(page.getByRole('menuitem', { exact: true, name: 'Manage Organization' })).toBeVisible();
    await page.getByRole('menuitem', { exact: true, name: 'Billing' }).click();
    await expect(page).toHaveURL(new RegExp(`/organization/${impersonatedOrganization.organizationId}/billing$`));
    await page.getByRole('button').filter({ hasText: currentUser!.email_address! }).click();
    await page.getByRole('menuitem', { exact: true, name: 'Manage Organization' }).click();
    await expect(page).toHaveURL(new RegExp(`/organization/${impersonatedOrganization.organizationId}/manage$`));
});

test('project notifications use the selected organization, including impersonation', async ({
    e2eScenario,
    e2eSecondaryOrganization,
    impersonatedOrganization,
    page
}) => {
    const projectPicker = page.locator('[data-slot="select-trigger"]');
    await page.goto(`/next/account/notifications?project=${e2eScenario.projectId}`);
    await expect(projectPicker).toHaveText(e2eScenario.projectName);
    await projectPicker.click();
    await expect(page.getByRole('option', { name: e2eSecondaryOrganization.projectName })).toBeHidden();
    await page.keyboard.press('Escape');

    await page.goto(`/next/organization/${impersonatedOrganization.organizationId}/manage`);
    await expect(page.getByRole('heading', { name: `${impersonatedOrganization.organizationName} Settings` })).toBeVisible();
    await page.goto(`/next/account/notifications?project=${e2eScenario.projectId}`);
    await expect(projectPicker).toHaveText(impersonatedOrganization.projectName);
    await projectPicker.click();
    await expect(page.getByRole('option', { name: e2eScenario.projectName })).toBeHidden();
    await page.keyboard.press('Escape');
    await page.reload();
    await expect(projectPicker).toHaveText(impersonatedOrganization.projectName);
});

test('global administrators can remain in an impersonated organization without memberships', async ({ impersonatedOrganization, page }) => {
    await page.route('**/api/v2/users/me', async (route) => {
        const response = await route.fetch();
        const user = await response.json();
        await route.fulfill({ json: { ...user, organization_ids: [] }, response });
    });
    await page.route(/\/api\/v2\/organizations(?:\?.*)?$/, async (route) => {
        await route.fulfill({ json: [] });
    });
    await page.goto(`/next/organization/${impersonatedOrganization.organizationId}/manage`);
    await expect(page.getByRole('heading', { name: `${impersonatedOrganization.organizationName} Settings` })).toBeVisible();
    await expect(page.getByRole('button').filter({ hasText: impersonatedOrganization.organizationName }).filter({ hasText: 'Impersonating' })).toBeVisible();
    await page.goto('/next/project/list');
    await expect(getVisibleText(page, impersonatedOrganization.projectName)).toBeVisible();
    await page.reload();
    await expect(getVisibleText(page, impersonatedOrganization.projectName)).toBeVisible();
});

test('impersonated organization changes arrive through the live connection', async ({ e2eApi, impersonatedOrganization, page }) => {
    await page.goto(`/next/organization/${impersonatedOrganization.organizationId}/manage`);
    await expect(page.getByRole('heading', { name: `${impersonatedOrganization.organizationName} Settings` })).toBeVisible();
    const receivedOrganizations: string[] = [];
    page.on('websocket', (socket) => {
        if (!socket.url().includes('/api/v2/push')) {
            return;
        }
        socket.on('framereceived', ({ payload }) => {
            const envelope = JSON.parse(String(payload)) as { message?: { organization_id?: string } };
            if (envelope.message?.organization_id) {
                receivedOrganizations.push(envelope.message.organization_id);
            }
        });
    });
    const socketConnected = page.waitForEvent('websocket', (socket) => socket.url().includes('/api/v2/push'));
    await page.reload();
    await socketConnected;
    await expect(page.getByRole('heading', { name: `${impersonatedOrganization.organizationName} Settings` })).toBeVisible();
    await seedRepresentativeEvent(e2eApi, impersonatedOrganization.ownerToken, impersonatedOrganization);
    await expect.poll(() => receivedOrganizations, { timeout: 15_000 }).toContain(impersonatedOrganization.organizationId);
});

test('dashboards and project filters use the impersonated organization', async ({ e2eApi, e2eScenario, impersonatedOrganization, page }) => {
    await Promise.all([
        seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eScenario),
        seedRepresentativeEvent(e2eApi, impersonatedOrganization.ownerToken, impersonatedOrganization)
    ]);
    await page.goto(`/next/organization/${impersonatedOrganization.organizationId}/manage`);
    await expect(page.getByRole('heading', { name: `${impersonatedOrganization.organizationName} Settings` })).toBeVisible();

    for (const path of ['event', 'stack', 'stream']) {
        await page.goto(`/next/${path}?time=all`);
        await expect(getVisibleText(page, impersonatedOrganization.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eScenario.message)).toBeHidden();
    }

    await page.goto('/next/event?time=all');
    await page.getByRole('button', { name: 'Manage filters' }).click();
    await page.getByPlaceholder('Search...').fill('Project');
    await page.getByRole('option', { exact: true, name: 'Project' }).click();
    await expect(page.getByRole('option', { name: impersonatedOrganization.projectName })).toBeVisible();
    await expect(page.getByRole('option', { name: e2eScenario.projectName })).toBeHidden();
});

test('Sessions use the impersonated organization', async ({ e2eApi, e2eScenario, impersonatedOrganization, page }) => {
    for (const scope of [e2eScenario, { ...impersonatedOrganization, userToken: impersonatedOrganization.ownerToken }]) {
        await e2eApi.submitEvent(
            scope.projectId,
            scope.projectToken,
            createSessionEvent({
                identity: `${scope.projectId}@exceptionless.test`,
                name: scope.projectName,
                sessionId: scope.referenceId
            })
        );
        await e2eApi.pollForEventByReference(scope.userToken, scope.projectId, scope.referenceId);
    }
    await page.goto(`/next/organization/${impersonatedOrganization.organizationId}/manage`);
    await expect(page.getByRole('heading', { name: `${impersonatedOrganization.organizationName} Settings` })).toBeVisible();
    await page.goto('/next/sessions?time=all');
    await expect(getVisibleText(page, impersonatedOrganization.projectName)).toBeVisible({ timeout: 30_000 });
    await expect(getVisibleText(page, e2eScenario.projectName)).toBeHidden();
});

test('Event Stream clears old rows when another tab changes the organization', async ({ e2eApi, e2eScenario, impersonatedOrganization, page }) => {
    await Promise.all([
        seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eScenario),
        seedRepresentativeEvent(e2eApi, impersonatedOrganization.ownerToken, impersonatedOrganization)
    ]);
    await page.goto('/next/stream');
    await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });

    const organizationTab = await page.context().newPage();
    try {
        await organizationTab.goto('/next/status');
        await organizationTab.evaluate(
            (organizationId) => window.localStorage.setItem('organization', JSON.stringify(organizationId)),
            impersonatedOrganization.organizationId
        );
        await expect(getVisibleText(page, impersonatedOrganization.message)).toBeVisible({ timeout: 30_000 });
        await expect(getVisibleText(page, e2eScenario.message)).toBeHidden();
    } finally {
        await organizationTab.close();
    }
});
