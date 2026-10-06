import { expect, type Page, test } from '@playwright/test';

const id = (value: number) => value.toString(16).padStart(24, '0');

// All account data and mutations in this journey are intercepted in the browser.
test('Organizations_WithOrderedApiResultsAndDuplicateNames_KeepOrderAndSelectionAfterMutations', async ({ baseURL, page }) => {
    // Arrange
    page.setDefaultTimeout(10_000);
    expect(['localhost', '127.0.0.1', 'web-ex.dev.localhost']).toContain(new URL(baseURL!).hostname);
    const organizations = ['Zulu Organization', 'Beta Organization', 'Alpha Organization'].map((name, index) => ({
        features: [],
        has_premium_features: true,
        id: id(index + 1),
        name,
        plan_id: 'EX_UNLIMITED',
        plan_name: 'Unlimited'
    }));
    let project = { id: id(90), is_configured: true, name: 'Ordering Project', organization_id: id(1) };
    await page.addInitScript((organizationId) => {
        localStorage.setItem('satellizer_token', 'synthetic-ordering-token');
        if (!localStorage.getItem('organization')) {
            localStorage.setItem('organization', JSON.stringify(organizationId));
        }
    }, id(1));
    await page.routeWebSocket('**/api/v2/push*', () => {});
    await page.route('**/*', async (route) => {
        const request = route.request();
        const url = new URL(request.url());
        if (url.origin !== new URL(baseURL!).origin) {
            await route.abort();
            return;
        }
        const path = url.pathname;
        if (path === '/health') {
            await route.fulfill({ body: 'OK' });
        } else if (!path.startsWith('/api/')) {
            await route.continue();
        } else if (path === '/api/v2/users/me') {
            await route.fulfill({
                json: {
                    email_address: 'ordering@example.test',
                    full_name: 'Ordering Tester',
                    has_local_account: true,
                    id: id(80),
                    is_active: true,
                    is_email_address_verified: true,
                    is_invite: false,
                    o_auth_accounts: [],
                    organization_ids: organizations.map((organization) => organization.id),
                    organization_preferences: [],
                    product_tours: { app_welcome: '2026-10-01T12:00:00Z' },
                    roles: []
                }
            });
        } else if (path === '/api/v2/organizations' && request.method() === 'POST') {
            const created = { ...organizations[0], id: id(22), name: request.postDataJSON().name as string };
            organizations.push(created);
            await route.fulfill({ json: created });
        } else if (path === '/api/v2/organizations') {
            const filter = url.searchParams.get('filter')?.toLowerCase();
            // Model the API's name-ascending response; backend coverage verifies the actual ordering.
            const results = filter ? organizations.filter((organization) => organization.name.toLowerCase().includes(filter)) : organizations;
            await route.fulfill({ json: results.toSorted((left, right) => left.name.localeCompare(right.name)) });
        } else if (/^\/api\/v2\/organizations\/[a-f0-9]+$/.test(path)) {
            const organization = organizations.find((candidate) => candidate.id === path.split('/').at(-1));
            if (request.method() === 'PATCH') {
                Object.assign(organization!, request.postDataJSON());
            }
            await route.fulfill({ json: organization });
        } else if (path.endsWith('/projects')) {
            if (request.method() === 'POST') {
                project = { ...project, ...request.postDataJSON() };
                await route.fulfill({ json: project });
            } else {
                await route.fulfill({ json: [project] });
            }
        } else if (path === `/api/v2/projects/${project.id}`) {
            await route.fulfill({ json: project });
        } else if (path === '/api/v2/assistant/access') {
            await route.fulfill({ json: { enabled: false, has_access: false, upgrade_required: false } });
        } else if (path.endsWith('/events/count')) {
            await route.fulfill({ json: { aggregations: {}, total: 0 } });
        } else {
            await route.fulfill({ json: [] });
        }
    });
    const rows = page.locator('tbody > tr:visible').locator('xpath=descendant::td[1]');
    const menuNames = () => page.getByRole('menuitem').filter({ has: page.locator('[title]') });
    const assertSelected = (organizationId: string) =>
        expect.poll(() => page.evaluate(() => JSON.parse(localStorage.getItem('organization') ?? 'null'))).toBe(organizationId);

    // Act: Settings and the sidebar consume the API's default order.
    await page.goto('/organization/list');
    // Assert
    await expect(rows).toHaveText(['Alpha Organization', 'Beta Organization', 'Zulu Organization']);

    await test.step('switch and reload without changing the selected ID', async () => {
        // Act
        await openSwitcher(page, 'Zulu Organization');
        // Assert
        await expect(menuNames()).toHaveText([/Alpha Organization$/, /Beta Organization$/, /Zulu Organization$/]);
        await page.getByRole('menuitem').filter({ hasText: 'Alpha Organization' }).click();
        await assertSelected(id(3));
        await page.reload();
        await openSwitcher(page, 'Alpha Organization');
        await expect(menuNames()).toHaveText([/Alpha Organization$/, /Beta Organization$/, /Zulu Organization$/]);
        await assertSelected(id(3));
        await page.keyboard.press('Escape');
    });

    await test.step('rename to a duplicate name and retain both stable IDs', async () => {
        // Act
        await page.goto(`/organization/${id(3)}/manage`);
        const refreshed = page.waitForResponse(
            (response) => new URL(response.url()).pathname === '/api/v2/organizations' && response.request().method() === 'GET'
        );
        const patched = page.waitForResponse((response) => response.url().endsWith(`/organizations/${id(3)}`) && response.request().method() === 'PATCH');
        await page.getByLabel('Organization name', { exact: true }).fill('Zulu Organization');
        await patched;
        await refreshed;
        await openSwitcher(page, 'Zulu Organization');
        // Assert
        await expect(menuNames()).toHaveText([/Beta Organization$/, /Zulu Organization$/, /Zulu Organization$/]);
        const duplicates = page.getByRole('menuitem').filter({ hasText: 'Zulu Organization' });
        await expect(duplicates).toHaveCount(2);
        await expect(duplicates.nth(1)).toHaveAttribute('data-current-organization', 'true');
        await duplicates.first().click();
        await assertSelected(id(1));
    });

    await test.step('create an organization and re-sort cached and refetched lists', async () => {
        // Act
        await page.goto('/organization/add');
        await page.getByLabel('Organization Name', { exact: true }).fill('AAA Created Organization');
        await page.getByLabel('Project Name', { exact: true }).fill('Created Ordering Project');
        await page.getByRole('button', { exact: true, name: 'Continue' }).click();
        await expect(page).toHaveURL(/\/configure/);
        await openSwitcher(page, 'AAA Created Organization');
        // Assert
        await expect(menuNames()).toHaveText([/AAA Created Organization$/, /Beta Organization$/, /Zulu Organization$/, /Zulu Organization$/]);
        await assertSelected(id(22));
        await page.keyboard.press('Escape');
        await page.goto('/organization/list');
        await expect(rows).toHaveText(['AAA Created Organization', 'Beta Organization', 'Zulu Organization', 'Zulu Organization']);
        await assertSelected(id(22));
    });
    expect(await page.pageErrors()).toEqual([]);
});

async function openSwitcher(page: Page, name: string): Promise<void> {
    await page.getByRole('button').filter({ hasText: name }).first().click();
    await expect(page.getByRole('menu')).toBeVisible();
}
