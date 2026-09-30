import { expect, type Page, type Route, test } from '@playwright/test';

const ORGANIZATION_ID = '000000000000000000000001';
const OTHER_ORGANIZATION_ID = '000000000000000000000004';

test('complete tag suggestions filter locally without changing selected tags', async ({ page }) => {
    const requests: string[] = [];
    const input = page.getByPlaceholder('Tag', { exact: true });
    await test.step('arrange complete suggestions and an existing selection', async () => {
        await page.clock.install();
        await setup(page, async (route, aggregation) => {
            requests.push(aggregation);
            await route.fulfill({ json: tags(['Alpha', 'Beta']) });
        });
    });
    await test.step('filter a complete cache locally while preserving the selection', async () => {
        await page.goto('/next/event?tag=Selected&time=%5Bnow-24h%20TO%20now%5D&project=000000000000000000000003');
        await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
        await expect(page.getByRole('option', { exact: true, name: 'Alpha' })).toBeVisible();
        await input.fill('Be');
        await expect(page.getByRole('option', { exact: true, name: 'Beta' })).toBeVisible();
        await expect(page.getByRole('option', { exact: true, name: 'Alpha' })).toHaveCount(0);
        await page.clock.fastForward(450);
        expect(requests).toEqual(['terms:(tags~251)']);
        await expect(page).toHaveURL(/[?&]tag=Selected/);
    });
    await test.step('reopen with an empty search and cached options', async () => {
        await input.press('Escape');
        await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
        await expect(input).toHaveValue('');
        await expect(page.getByRole('option', { exact: true, name: 'Alpha' })).toBeVisible();
        expect(requests).toHaveLength(1);
        await input.press('Escape');
    });
    await test.step('reuse suggestions after date and project filters change', async () => {
        await page.getByRole('button', { exact: true, name: 'Date Last 24 hours' }).click();
        await page.getByRole('button', { exact: true, name: 'Last 7 days' }).click();
        await expect(page.getByRole('button', { exact: true, name: 'Date Last 7 days' })).toBeVisible();
        await expect(page).not.toHaveURL(/[?&]time=/);
        await page
            .getByRole('button', { name: /^Project/ })
            .first()
            .click();
        const projectSearch = page.getByPlaceholder('Project', { exact: true });
        await projectSearch.fill('One');
        await projectSearch.press('Escape');
        await page
            .getByRole('button', { name: /^Project/ })
            .first()
            .click();
        await expect(projectSearch).toHaveValue('');
        await page.getByRole('option', { exact: true, name: 'Project One' }).click();
        await expect(page).not.toHaveURL(/[?&]project=/);
        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
        await page.clock.fastForward(450);
        expect(requests).toHaveLength(1);
    });
});

test('incomplete suggestions debounce remote search, reuse cache and preserve selections through failure', async ({ page }) => {
    const requests: string[] = [];
    const input = page.getByPlaceholder('Tag', { exact: true });
    let searchFailed = false;
    await test.step('arrange truncated suggestions and one transient search failure', async () => {
        await page.clock.install();
        await setup(page, async (route, aggregation) => {
            requests.push(aggregation);
            if (aggregation === 'terms:(tags~251)') {
                await route.fulfill({ json: tags(['Common'], 1) });
            } else if (aggregation.includes('[fF][aA][iI][lL]')) {
                if (!searchFailed) {
                    searchFailed = true;
                    await route.fulfill({ json: { status: 503, title: 'Unavailable' }, status: 503 });
                } else {
                    await route.fulfill({ json: tags(['Failover']) });
                }
            } else {
                await route.fulfill({ json: tags(['RareTag']) });
            }
        });
    });
    await test.step('debounce searches and add a tag without dropping the selection', async () => {
        await page.goto('/next/event?tag=Selected');
        await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
        await expect(page.getByRole('option', { exact: true, name: 'Common' })).toBeVisible();
        await input.fill('R');
        await page.clock.fastForward(350);
        expect(requests).toHaveLength(1);
        await input.fill('Ra');
        await input.fill('Rar');
        await input.fill('Rare');
        await page.clock.fastForward(350);
        await expect(page.getByRole('option', { exact: true, name: 'RareTag' })).toBeVisible();
        expect(requests).toHaveLength(2);
        await page.getByRole('option', { exact: true, name: 'RareTag' }).click();
        await expect(page).toHaveURL(/RareTag/);
        expect(requests).toHaveLength(2);
    });
    await test.step('preserve selections through failure and explicit retry', async () => {
        await input.fill('fail');
        await page.clock.fastForward(350);
        await expect(page.getByText('Could not load tags.')).toBeVisible();
        await expect(page.getByRole('button', { exact: true, name: 'Retry' })).toBeVisible();
        await expect(page).toHaveURL(/Selected/);
        await expect(page).toHaveURL(/RareTag/);
        await page.getByRole('button', { exact: true, name: 'Retry' }).click();
        await expect(page.getByRole('option', { exact: true, name: 'Failover' })).toBeVisible();
    });
    await test.step('reuse the previously fetched search', async () => {
        await input.fill('Rare');
        await page.clock.fastForward(350);
        await expect(page.getByRole('option', { exact: true, name: 'RareTag' })).toBeVisible();
        await page.clock.fastForward(350);
        expect(requests).toHaveLength(4);
    });
});

test('initial failure retries the initial query and keeps saved tag selections', async ({ page }) => {
    const requests: string[] = [];
    await setup(page, async (route, aggregation) => {
        requests.push(aggregation);
        if (requests.length === 1) {
            await route.fulfill({ json: { status: 503, title: 'Unavailable' }, status: 503 });
        } else {
            await route.fulfill({ json: tags(['Alpha']) });
        }
    });
    await page.goto('/next/event?tag=Selected');
    await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
    await expect(page.getByText('Could not load tags.')).toBeVisible();
    await page.getByRole('button', { exact: true, name: 'Retry' }).click();
    await expect(page.getByRole('option', { exact: true, name: 'Alpha' })).toBeVisible();
    await expect(page).toHaveURL(/[?&]tag=Selected/);
    expect(requests).toEqual(['terms:(tags~251)', 'terms:(tags~251)']);
});

test('closing during debounce cancels the search and tag actions keep working', async ({ page }) => {
    const requests: string[] = [];
    await page.clock.install();
    await setup(page, async (route, aggregation) => {
        requests.push(aggregation);
        await route.fulfill({ json: tags(['Common'], 1) });
    });
    await page.goto('/next/event?tag=Selected');
    await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
    await expect(page.getByRole('option', { exact: true, name: 'Common' })).toBeVisible();
    const input = page.getByPlaceholder('Tag', { exact: true });
    await input.fill('rare');
    await input.press('Escape');
    await page.clock.fastForward(450);
    expect(requests).toHaveLength(1);
    await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
    await expect(input).toHaveValue('');
    await page.getByRole('button', { exact: true, name: 'Clear filter value' }).click();
    await expect(page).not.toHaveURL(/[?&]tag=Selected/);
    await page.getByRole('option', { exact: true, name: 'Common' }).click();
    await expect(page).toHaveURL(/[?&]tag=Common/);
    await page.getByRole('button', { exact: true, name: 'Remove filter' }).click();
    await expect(page.getByRole('button', { name: /^Tag\s/ })).toHaveCount(0);
    await expect(page).not.toHaveURL(/[?&]tag=Common/);
});

test('switching organizations discards the old picker and loads only the new organization tags', async ({ page }) => {
    const requestedOrganizations: string[] = [];
    await setup(page, async (route) => {
        const organizationId = new URL(route.request().url()).pathname.split('/')[4]!;
        requestedOrganizations.push(organizationId);
        await route.fulfill({ json: tags([organizationId === ORGANIZATION_ID ? 'FirstOrganizationTag' : 'SecondOrganizationTag']) });
    });
    await page.goto('/next/event?tag=Selected');
    await page.getByRole('button', { name: /^Tag\s+Selected/ }).click();
    await expect(page.getByRole('option', { exact: true, name: 'FirstOrganizationTag' })).toBeVisible();
    await page.getByPlaceholder('Tag', { exact: true }).press('Escape');
    await page.getByRole('button', { name: /Test Organization.*Unlimited/ }).click();
    await page.getByRole('menuitem', { name: /Other Organization/ }).click();
    await expect(page.getByRole('button', { name: /Other Organization.*Unlimited/ })).toBeVisible();
    await page.goto('/next/event?tag=OtherSelected');
    await page.getByRole('button', { name: /^Tag\s+OtherSelected/ }).click();
    await expect(page.getByRole('option', { exact: true, name: 'SecondOrganizationTag' })).toBeVisible();
    await expect(page.getByRole('option', { exact: true, name: 'FirstOrganizationTag' })).toHaveCount(0);
    await expect(page.getByRole('option', { exact: true, name: 'Selected' })).toHaveCount(0);
    expect(requestedOrganizations).toEqual([ORGANIZATION_ID, OTHER_ORGANIZATION_ID]);
});

async function setup(page: Page, handleTags: (route: Route, aggregation: string) => Promise<void>) {
    page.setDefaultTimeout(10000);
    await page.addInitScript((organizationId) => {
        localStorage.setItem('satellizer_token', 'synthetic-tag-test-token');
        if (!localStorage.getItem('organization')) {
            localStorage.setItem('organization', JSON.stringify(organizationId));
        }
    }, ORGANIZATION_ID);
    await page.route('**/health', (route) => route.fulfill({ body: 'OK' }));
    await page.route('**/api/v2/**', async (route) => {
        const url = new URL(route.request().url());
        const aggregation = url.searchParams.get('aggregations');
        if (aggregation?.startsWith('terms:(tags~')) {
            expect([ORGANIZATION_ID, OTHER_ORGANIZATION_ID]).toContain(url.pathname.split('/')[4]);
            expect(url.searchParams.get('filter')).toBeNull();
            expect(url.searchParams.get('time')).toBe('all');
            await handleTags(route, aggregation);
        } else if (url.pathname === '/api/v2/users/me') {
            await route.fulfill({
                json: {
                    email_address: 'tags@example.test',
                    full_name: 'Test User',
                    id: '000000000000000000000002',
                    is_active: true,
                    is_email_address_verified: true,
                    organization_ids: [ORGANIZATION_ID, OTHER_ORGANIZATION_ID],
                    organization_preferences: [],
                    roles: []
                }
            });
        } else if (url.pathname === '/api/v2/organizations' || /^\/api\/v2\/organizations\/[^/]+$/.test(url.pathname)) {
            const organizations = [
                { features: [], id: ORGANIZATION_ID, name: 'Test Organization', plan_id: 'EX_UNLIMITED', plan_name: 'Unlimited' },
                { features: [], id: OTHER_ORGANIZATION_ID, name: 'Other Organization', plan_id: 'EX_UNLIMITED', plan_name: 'Unlimited' }
            ];
            await route.fulfill({
                json: url.pathname === '/api/v2/organizations' ? organizations : organizations.find((item) => url.pathname.endsWith(item.id))
            });
        } else if (url.pathname.endsWith('/projects')) {
            await route.fulfill({ json: [{ id: '000000000000000000000003', name: 'Project One', organization_id: ORGANIZATION_ID }] });
        } else if (url.pathname === '/api/v2/assistant/access') {
            await route.fulfill({ json: { enabled: false, has_access: false } });
        } else if (url.pathname.endsWith('/count')) {
            await route.fulfill({ json: { aggregations: {}, total: 0 } });
        } else {
            await route.fulfill({ json: [] });
        }
    });
}

function tags(values: string[], omitted = 0) {
    return {
        aggregations: {
            terms_tags: {
                data: { '@type': 'bucket', ...(omitted ? { SumOtherDocCount: omitted } : {}) },
                items: values.map((key) => ({ key, total: 1 }))
            }
        },
        total: values.length
    };
}
