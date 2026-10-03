import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';

test.skip(process.env.E2E_ENV === 'production', 'Cutover coverage targets the local app.');

test('primary old links retain the session and require confirmation before applying stack actions', async ({ e2eApi, e2eScenario, page }) => {
    const event = await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eScenario);
    const mutations: string[] = [];
    page.on('request', (request) => {
        const path = new URL(request.url()).pathname;
        if ((path.includes('/stacks/') && request.method() !== 'GET') || path.endsWith('/auth/logout')) mutations.push(path);
    });

    for (const link of [`/event/${event.id}`, `/#!/event/by-ref/${e2eScenario.referenceId}`]) {
        await page.goto(link);
        await expect(page).toHaveURL(new RegExp(`/stack/${event.stack_id}/event/${event.id}$`));
        await expect(page.getByText(e2eScenario.message, { exact: true }).filter({ visible: true }).first()).toBeVisible();
        expect(await page.evaluate(() => localStorage.getItem('satellizer_token'))).toBe(e2eScenario.userToken);
    }
    for (const [link, heading] of [
        [`/stack/${event.stack_id}/mark-fixed`, 'Mark Stack As Fixed'],
        [`/#/stack/${event.stack_id}/ignored`, 'Ignore Stack'],
        [`/stack/${event.stack_id}/discarded`, 'Discard Stack']
    ]) {
        await page.goto(link);
        const dialog = page.getByRole('alertdialog');
        await expect(dialog.getByRole('heading', { exact: true, name: heading })).toBeVisible();
        await expect(dialog.getByText(e2eScenario.message, { exact: true })).toBeVisible();
        expect(mutations).toEqual([]);
        await dialog.getByRole('button', { exact: true, name: 'Cancel' }).click();
        await expect(page).toHaveURL(new RegExp(`/stack/${event.stack_id}/event/${event.id}$`));
        await page.reload();
        await expect(page.getByRole('button', { exact: true, name: 'Open' })).toBeVisible();
        await expect(dialog).not.toBeVisible();
        expect(await page.evaluate(() => localStorage.getItem('satellizer_token'))).toBe(e2eScenario.userToken);
    }
    expect(mutations).toEqual([]);
});

test('old project and organization dashboards select the destination organization', async ({ e2eApi, e2eScenario, e2eSecondaryOrganization, page }) => {
    await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eSecondaryOrganization);
    await page.goto('/stack/all');
    await expect(page.getByRole('heading', { name: 'All' })).toBeVisible();

    await page.goto(`/#!/project/${e2eSecondaryOrganization.projectId}/error/timeline?time=all`);
    await expect.poll(() => new URL(page.url()).pathname).toBe('/event');
    expect(new URL(page.url()).searchParams.get('project')).toBe(e2eSecondaryOrganization.projectId);
    await expect(page.getByText(e2eSecondaryOrganization.message, { exact: true }).filter({ visible: true }).first()).toBeVisible();
    expect(await page.evaluate(() => JSON.parse(localStorage.getItem('organization') ?? 'null'))).toBe(e2eSecondaryOrganization.organizationId);

    await page.goto(`/organization/${e2eScenario.organizationId}/frequent?time=all`);
    await expect.poll(() => new URL(page.url()).pathname).toBe('/stack');
    expect(await page.evaluate(() => JSON.parse(localStorage.getItem('organization') ?? 'null'))).toBe(e2eScenario.organizationId);

    await page.goto(`/#!/organization/${e2eScenario.organizationId}/upgrade?source=email`);
    await expect(page).toHaveURL(`/organization/${e2eScenario.organizationId}/billing?source=email&changePlan=true`);
    await expect(page.getByRole('dialog').getByRole('heading', { exact: true, name: 'Manage subscription' })).toBeVisible();
});

test('OAuth popup responses remain readable without signing out the existing session', async ({ e2eScenario, page }) => {
    const logouts: string[] = [];
    page.on('request', (request) => {
        if (new URL(request.url()).pathname.endsWith('/auth/logout')) logouts.push(request.url());
    });
    for (const callback of ['/?code=test-code&state=test-state', '/?error=access_denied&state=test-state', '/#access_token=test-token&state=test-state']) {
        await page.goto(callback);
        await expect(page.getByText('Completing sign in…')).toBeVisible();
        expect(new URL(page.url()).pathname + new URL(page.url()).search + new URL(page.url()).hash).toBe(callback);
        expect(await page.evaluate(() => localStorage.getItem('satellizer_token'))).toBe(e2eScenario.userToken);
    }
    expect(logouts).toEqual([]);
});

test.describe('in-app legacy navigation', () => {
    test.use({ e2eUseInvitedUser: true });

    test('Back normalizes an existing legacy history entry without losing Forward', async ({ e2eScenario, page }) => {
        await page.goto('/stack/all');
        await expect(page.getByRole('heading', { exact: true, name: 'All' })).toBeVisible();
        await page.goto(`/account/notifications?project=${e2eScenario.projectId}`);
        await expect(page.getByRole('heading', { exact: true, name: 'Project Notifications' })).toBeVisible();
        const destination = page.url();

        await page.evaluate((oldUrl) => {
            window.history.replaceState(window.history.state, '', oldUrl);
            const link = document.createElement('a');
            link.href = '/project/list';
            link.textContent = 'Open project list';
            document.querySelector('main')!.prepend(link);
        }, `/account/manage?projectId=${e2eScenario.projectId}&tab=notifications`);
        await page.getByRole('link', { exact: true, name: 'Open project list' }).click();
        await expect(page).toHaveURL(/\/project\/list$/);

        await page.goBack();
        await expect(page).toHaveURL(destination);
        await expect(page.getByRole('heading', { exact: true, name: 'Project Notifications' })).toBeVisible();
        await page.goForward();
        await expect(page).toHaveURL(/\/project\/list$/);
        await page.goBack();
        await expect(page).toHaveURL(destination);
        await page.goBack();
        await expect(page).toHaveURL(/\/stack\/all$/);
        expect(await page.evaluate(() => localStorage.getItem('satellizer_token'))).toBe(e2eScenario.userToken);
    });

    test('following a historical hash settings link preserves browser back and forward navigation', async ({ e2eScenario, page }) => {
        await page.goto('/stack/all');
        await expect(page.getByRole('heading', { exact: true, name: 'All' })).toBeVisible();
        await page.goto('/project/list');
        await expect(page.getByRole('heading', { exact: true, name: 'Projects' })).toBeVisible();

        // Exercise a historical saved link through the app router, without reloading the document.
        await page.evaluate((projectId) => {
            const link = document.createElement('a');
            link.href = `/#!/account/manage?projectId=${projectId}&tab=notifications&from=legacy-link`;
            link.textContent = 'Historical notification settings';
            document.querySelector('main')!.prepend(link);
        }, e2eScenario.projectId);
        await page.getByRole('link', { exact: true, name: 'Historical notification settings' }).click();

        await expect(page.getByRole('heading', { exact: true, name: 'Project Notifications' })).toBeVisible();
        await expect(page.getByRole('button', { exact: true, name: e2eScenario.projectName })).toBeVisible();
        const destination = page.url();
        expect(new URL(destination).pathname).toBe('/account/notifications');
        expect(new URL(destination).searchParams.get('from')).toBe('legacy-link');

        await page.goBack();
        await expect(page).toHaveURL(/\/project\/list$/);
        await expect(page.getByRole('heading', { exact: true, name: 'Projects' })).toBeVisible();

        await page.goForward();
        await expect(page).toHaveURL(destination);
        await expect(page.getByRole('heading', { exact: true, name: 'Project Notifications' })).toBeVisible();
        expect(await page.evaluate(() => localStorage.getItem('satellizer_token'))).toBe(e2eScenario.userToken);
    });
});
