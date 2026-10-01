import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';

test('old next links retain query and fragment through authentication', async ({ page }) => {
    const destination = '/account/notifications?project=123&from=email%2Blink#notification-settings';
    await page.goto(`/next${destination}`);
    await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
    await expect.poll(() => new URL(page.url()).searchParams.get('redirect')).toBe(destination);
});

test('root OAuth popup callbacks retain their parameters for the opener after reload', async ({ page }) => {
    await page.goto('/login');
    const popupPromise = page.waitForEvent('popup');
    await page.evaluate(() => window.open('/?code=root-callback-code&state=root-callback-state', 'oauth-cutover-test'));
    const popup = await popupPromise;
    await expect(popup.getByRole('status')).toHaveText('Completing sign in...');
    await expect(popup).toHaveURL(/\/\?code=root-callback-code&state=root-callback-state$/);
    await popup.reload();
    await expect(popup.getByRole('status')).toHaveText('Completing sign in...');
    expect(new URL(popup.url()).searchParams.get('code')).toBe('root-callback-code');
    await popup.close();
});

test('old hash notifications select their organization and retain the session', async ({ e2eScenario, e2eSecondaryOrganization, page }) => {
    await page.goto(`/#!/organization/${e2eSecondaryOrganization.organizationId}/dashboard`);
    await expect.poll(() => page.evaluate(() => JSON.parse(localStorage.getItem('organization') ?? 'null'))).toBe(e2eSecondaryOrganization.organizationId);
    await expect(page).toHaveURL(/\/event(?:[?#]|$)/);
    await page.reload();
    await expect(page.getByRole('button').filter({ hasText: e2eSecondaryOrganization.organizationName }).filter({ visible: true }).first()).toBeVisible();
    expect(await page.evaluate(() => localStorage.getItem('satellizer_token'))).toBe(e2eScenario.userToken);
});

for (const [action, title] of [
    ['fixed', 'Mark Stack As Fixed'],
    ['ignored', 'Ignore Stack'],
    ['discarded', 'Discard Stack']
]) {
    test(`stack ${action} notification opens its confirmation without changing status`, async ({ e2eApi, e2eScenario, page }) => {
        const event = await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: e2eScenario.message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: e2eScenario.referenceId
        });
        const writes: string[] = [];
        page.on('request', (request) => {
            if (request.method() !== 'GET' && request.url().includes('/api/v2/stacks/')) {
                writes.push(request.url());
            }
        });
        await page.goto(`/next/stack/${event.stack_id}?action=${action}`);
        await expect(page.getByRole('alertdialog', { name: title })).toBeVisible();
        expect(writes).toEqual([]);
        await page.getByRole('button', { exact: true, name: 'Cancel' }).click();
        await expect(page.getByRole('alertdialog')).not.toBeVisible();
        await page.reload();
        await expect(page.getByText(e2eScenario.message, { exact: true }).filter({ visible: true }).first()).toBeVisible();
        await expect(page.getByRole('alertdialog')).not.toBeVisible();
        expect(writes).toEqual([]);
    });
}

test('newest stack notifications use the new-stack query mode after reload', async ({ e2eScenario, page }) => {
    const isNewest = (url: string) => {
        const parsed = new URL(url);
        return parsed.pathname.endsWith('/events') && parsed.searchParams.get('mode') === 'stack_new';
    };
    const response = page.waitForResponse((response) => isNewest(response.url()));
    await page.goto(`/stack?project=${e2eScenario.projectId}&mode=stack_new&type=error`);
    expect((await response).ok()).toBe(true);
    await expect(page).toHaveURL(/mode=stack_new/);
    const reloadResponse = page.waitForResponse((response) => isNewest(response.url()));
    await page.reload();
    expect((await reloadResponse).ok()).toBe(true);
});

test('project summary links select their organization and retain project scope', async ({ e2eScenario, e2eSecondaryOrganization, page }) => {
    for (const view of ['event', 'stack']) {
        await page.goto('/stack');
        await page.evaluate((id) => localStorage.setItem('organization', JSON.stringify(id)), e2eScenario.organizationId);
        const destination = `/${view}?organization=${e2eSecondaryOrganization.organizationId}&project=${e2eSecondaryOrganization.projectId}&type=error${view === 'stack' ? '&mode=stack_new' : ''}`;
        await page.goto(destination);
        await expect.poll(() => page.evaluate(() => JSON.parse(localStorage.getItem('organization') ?? 'null'))).toBe(e2eSecondaryOrganization.organizationId);
        await expect(page.getByRole('button').filter({ hasText: e2eSecondaryOrganization.projectName }).filter({ visible: true }).first()).toBeVisible();
        expect(new URL(page.url()).searchParams.get('project')).toBe(e2eSecondaryOrganization.projectId);
        if (view === 'stack') {
            expect(new URL(page.url()).searchParams.get('mode')).toBe('stack_new');
        }
        await page.reload();
        await expect(page.getByRole('button').filter({ hasText: e2eSecondaryOrganization.projectName }).filter({ visible: true }).first()).toBeVisible();
        expect(new URL(page.url()).searchParams.get('project')).toBe(e2eSecondaryOrganization.projectId);
    }

    await page.goto(`/event?organization=${e2eScenario.organizationId}&project=${e2eScenario.projectId}&type=error`);
    await expect.poll(() => page.evaluate(() => JSON.parse(localStorage.getItem('organization') ?? 'null'))).toBe(e2eScenario.organizationId);
    await page.evaluate((destination) => {
        const link = document.createElement('a');
        link.href = destination;
        document.body.append(link);
        link.click();
        link.remove();
    }, `/event?organization=${e2eSecondaryOrganization.organizationId}&project=${e2eSecondaryOrganization.projectId}&type=error`);
    await expect(page.getByRole('button').filter({ hasText: e2eSecondaryOrganization.projectName }).filter({ visible: true }).first()).toBeVisible();
    expect(new URL(page.url()).searchParams.get('project')).toBe(e2eSecondaryOrganization.projectId);
});

test.describe('legacy link browser history', () => {
    test.use({ e2eUseInvitedUser: true });

    for (const prefix of ['/next', '/#!', '/#', '']) {
        test(`following a ${prefix || 'root'} bookmark keeps the previous page and supports Forward`, async ({ e2eScenario, page }) => {
            await page.goto('/stack/all');
            await expect(page.getByRole('heading', { exact: true, name: 'All' })).toBeVisible();
            await page.goto('/project/list');
            await expect(page.getByRole('heading', { exact: true, name: 'Projects' })).toBeVisible();
            await page.evaluate((destination) => {
                const link = document.createElement('a');
                link.href = destination;
                link.textContent = 'Saved notification settings';
                document.querySelector('main')!.prepend(link);
            }, `${prefix}/account/manage?projectId=${e2eScenario.projectId}&tab=notifications&from=saved-link`);
            await page.getByRole('link', { exact: true, name: 'Saved notification settings' }).click();

            await expect(page.getByRole('heading', { exact: true, name: 'Project Notifications' })).toBeVisible();
            await expect(page.getByRole('button', { exact: true, name: e2eScenario.projectName })).toBeVisible();
            const destination = page.url();
            expect(new URL(destination).searchParams.get('from')).toBe('saved-link');
            await page.goBack();
            await expect(page).toHaveURL(/\/project\/list$/);
            await expect(page.getByRole('heading', { exact: true, name: 'Projects' })).toBeVisible();
            await page.goForward();
            await expect(page).toHaveURL(destination);
            await expect(page.getByRole('button', { exact: true, name: e2eScenario.projectName })).toBeVisible();
            expect(await page.evaluate(() => localStorage.getItem('satellizer_token'))).toBe(e2eScenario.userToken);
        });
    }

    test('Back normalizes an existing old history entry without adding another entry', async ({ e2eScenario, page }) => {
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
    });
});
