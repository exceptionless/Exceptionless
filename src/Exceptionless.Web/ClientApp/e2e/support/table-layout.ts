import type { Page, Route } from '@playwright/test';

const ORGANIZATION_ID = '000000000000000000000001';
const PROJECT_ID = '000000000000000000000002';
const USER_ID = '000000000000000000000003';

export async function mockTablePage(page: Page, rowCount: number): Promise<void> {
    await page.addInitScript(
        ({ organizationId }) => {
            window.localStorage.setItem('satellizer_token', 'table-layout-token');
            window.localStorage.setItem('organization', JSON.stringify(organizationId));
        },
        { organizationId: ORGANIZATION_ID }
    );
    await page.routeWebSocket('**/api/v2/push*', () => {});
    await page.route('**/health', (route) => route.fulfill({ body: 'OK', contentType: 'text/plain' }));
    await page.route('**/api/v2/**', (route) => fulfillTablePageRequest(route, rowCount));
}

async function fulfillTablePageRequest(route: Route, rowCount: number): Promise<void> {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const organization = {
        features: [],
        has_premium_features: true,
        id: ORGANIZATION_ID,
        name: 'Layout Organization',
        plan_id: 'EX_UNLIMITED',
        plan_name: 'Unlimited'
    };

    if (path === '/api/v2/users/me') {
        await route.fulfill({
            json: {
                email_address: 'layout@example.test',
                full_name: 'Layout Tester',
                has_local_account: true,
                id: USER_ID,
                is_active: true,
                is_email_address_verified: true,
                is_invite: false,
                o_auth_accounts: [],
                organization_ids: [ORGANIZATION_ID],
                organization_preferences: [],
                product_tours: { app_welcome: '2026-10-01T12:00:00Z' },
                roles: []
            }
        });
    } else if (path === '/api/v2/organizations') {
        await route.fulfill({ json: [organization] });
    } else if (path === `/api/v2/organizations/${ORGANIZATION_ID}`) {
        await route.fulfill({ json: organization });
    } else if (path === `/api/v2/organizations/${ORGANIZATION_ID}/projects`) {
        await route.fulfill({ json: [{ id: PROJECT_ID, is_configured: true, name: 'Layout Project', organization_id: ORGANIZATION_ID }] });
    } else if (path === '/api/v2/assistant/access') {
        await route.fulfill({ json: { enabled: false, has_access: false, message: null, upgrade_required: false } });
    } else if (path === '/api/v2/about') {
        await route.fulfill({ json: { informational_version: 'layout-test' } });
    } else if (path.endsWith('/events/count')) {
        await route.fulfill({ json: { aggregations: {}, total: rowCount } });
    } else if (path === `/api/v2/organizations/${ORGANIZATION_ID}/events` || path === `/api/v2/organizations/${ORGANIZATION_ID}/events/sessions`) {
        const limit = Number(url.searchParams.get('limit') ?? 20);
        const page = Number(url.searchParams.get('after') ?? url.searchParams.get('before') ?? url.searchParams.get('page') ?? 1);
        const isStack = url.searchParams.get('mode')?.startsWith('stack');
        const isSession = path.endsWith('/sessions');
        const rows = Array.from({ length: rowCount }, (_, index) => ({
            data: isSession
                ? { Name: `Session ${index + 1}`, SessionId: `session-${index + 1}`, Type: 'session' }
                : { Message: `Result ${index + 1}`, Type: 'error' },
            date: '2026-10-01T12:00:00Z',
            first_occurrence: '2026-10-01T12:00:00Z',
            id: (index + 100).toString(16).padStart(24, '0'),
            last_occurrence: '2026-10-01T12:00:00Z',
            project_id: PROJECT_ID,
            status: 'open',
            tags: [],
            template_key: isStack ? 'stack-summary' : isSession ? 'event-session-summary' : 'event-simple-summary',
            title: `Result ${index + 1}`,
            total: 1,
            total_users: 1,
            users: 1
        }));
        const links: string[] = [];
        if (page * limit < rowCount) {
            const next = new URL(url);
            next.searchParams.delete('before');
            next.searchParams.set('after', String(page + 1));
            links.push(`<${next}>; rel="next"`);
        }

        if (page > 1) {
            const previous = new URL(url);
            previous.searchParams.delete('after');
            previous.searchParams.set('before', String(page - 1));
            links.push(`<${previous}>; rel="previous"`);
        }

        await route.fulfill({ headers: { Link: links.join(', '), 'X-Result-Count': String(rowCount) }, json: rows.slice((page - 1) * limit, page * limit) });
    } else {
        await route.fulfill({ json: [] });
    }
}
