import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';

test.skip(process.env.E2E_ENV === 'production', 'Cutover coverage targets the local app.');

test('primary old links retain the session and open resources without applying stack actions', async ({ e2eApi, e2eScenario, page }) => {
    const event = await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eScenario);
    const mutations: string[] = [];
    page.on('request', (request) => {
        const path = new URL(request.url()).pathname;
        if ((path.includes('/stacks/') && request.method() !== 'GET') || path.endsWith('/auth/logout')) mutations.push(path);
    });

    for (const link of [
        `/next/event/${event.id}`,
        `/#!/event/by-ref/${e2eScenario.referenceId}`,
        `/stack/${event.stack_id}/mark-fixed`,
        `/#/stack/${event.stack_id}/ignored`,
        `/next/stack/${event.stack_id}/discarded`
    ]) {
        await page.goto(link);
        await expect(page).toHaveURL(new RegExp(`/stack/${event.stack_id}/event/${event.id}$`));
        await expect(page.getByText(e2eScenario.message, { exact: true }).filter({ visible: true }).first()).toBeVisible();
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
