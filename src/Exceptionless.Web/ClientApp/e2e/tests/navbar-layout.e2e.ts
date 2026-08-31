import { expect, type Route, test } from '@playwright/test';

const ORGANIZATION_ID = '000000000000000000000001';
const USER_ID = '000000000000000000000002';

test.use({ viewport: { height: 900, width: 820 } });

test('desktop navbar logo stays within the expanded sidebar', async ({ page }) => {
    await page.addInitScript(
        ({ organizationId }) => {
            window.localStorage.setItem('satellizer_token', 'navbar-layout-token');
            window.localStorage.setItem('organization', JSON.stringify(organizationId));
        },
        { organizationId: ORGANIZATION_ID }
    );

    await page.route('**/health', async (route) => {
        await route.fulfill({ body: 'OK', contentType: 'text/plain', status: 200 });
    });
    await page.route('**/api/v2/**', fulfillAppShellRequest);

    await page.goto('/next/stack');

    const logo = page.getByRole('link', { name: 'Exceptionless Logo' }).locator('img:visible');
    const sidebar = page.locator('[data-slot="sidebar-container"]');
    await expect(logo).toBeVisible();
    await expect(sidebar).toBeVisible();

    const logoBox = await logo.boundingBox();
    const sidebarBox = await sidebar.boundingBox();
    expect(logoBox).not.toBeNull();
    expect(sidebarBox).not.toBeNull();
    expect(logoBox!.x + logoBox!.width).toBeLessThanOrEqual(sidebarBox!.x + sidebarBox!.width);
});

async function fulfillAppShellRequest(route: Route): Promise<void> {
    const path = new URL(route.request().url()).pathname;

    if (path === '/api/v2/users/me') {
        await route.fulfill({
            json: {
                email_address: 'layout@example.test',
                email_notifications_enabled: true,
                full_name: 'Layout Tester',
                has_local_account: true,
                id: USER_ID,
                is_active: true,
                is_email_address_verified: true,
                is_invite: false,
                o_auth_accounts: [],
                organization_ids: [ORGANIZATION_ID],
                organization_preferences: [],
                roles: []
            }
        });
        return;
    }

    if (path === '/api/v2/organizations') {
        await route.fulfill({
            json: [{ features: [], id: ORGANIZATION_ID, name: 'Layout Organization', plan_id: 'EX_FREE', plan_name: 'Free' }]
        });
        return;
    }

    if (path === `/api/v2/organizations/${ORGANIZATION_ID}`) {
        await route.fulfill({
            json: { features: [], id: ORGANIZATION_ID, name: 'Layout Organization', plan_id: 'EX_FREE', plan_name: 'Free' }
        });
        return;
    }

    if (path === '/api/v2/assistant/access') {
        await route.fulfill({ json: { enabled: false, has_access: false, message: null, upgrade_required: false } });
        return;
    }

    await route.fulfill({ json: [] });
}
