import type { Locator } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';

// These responses are synthetic; the viewer needs no backend state for layout coverage.
const organization = { features: [], id: '000000000000000000000001', name: 'JSON wrapping', project_count: 1 };
const project = { data: {}, id: '000000000000000000000002', name: 'JSON wrapping', organization_id: organization.id, promoted_tabs: [] };
const request = {
    active: true,
    count: 42,
    items: [{ nested: { depth: 3 } }, null, 7],
    lines: 'first\nsecond',
    longField: 'unbroken'.repeat(60),
    markup: '<img src=x onerror="window.jsonMarkupExecuted=true"><script>window.jsonMarkupExecuted=true</script>',
    nullable: null
};
const events = [request, JSON.stringify(request)].map((value, index) => ({
    data: { Request: value },
    date: '2026-10-01T12:00:00Z',
    id: `00000000000000000000000${index + 3}`,
    message: `Synthetic JSON event ${index}`,
    organization_id: organization.id,
    project_id: project.id,
    type: 'log'
}));

test('Event JSON_With long object and string values_Wraps without changing displayed or copied data', async ({ baseURL, context, page }) => {
    // Arrange: isolate all application data and background connections from real services.
    const origin = new URL(baseURL!).origin;
    expect(new URL(origin).hostname).toMatch(/^(localhost|127\.0\.0\.1|.*\.localhost)$/);
    await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin });
    await page.addInitScript(
        ({ organizationId, origin }) => {
            localStorage.setItem('satellizer_token', 'synthetic-json-wrapping');
            localStorage.setItem('organization', JSON.stringify(organizationId));
            localStorage.setItem('PUBLIC_BASE_URL', origin);
            localStorage.setItem('mode-watcher-mode', 'light');
        },
        { organizationId: organization.id, origin }
    );
    await page.routeWebSocket('**/*', (socket) => {
        const url = new URL(socket.url());
        if (url.host === new URL(origin).host && !url.pathname.startsWith('/api/')) {
            socket.connectToServer();
        }
    });
    await page.route('**/*', async (route) => {
        const url = new URL(route.request().url());
        if (url.origin !== origin) {
            await route.abort();
            return;
        }
        if (url.pathname === '/health') {
            await route.fulfill({ body: 'Healthy' });
            return;
        }
        if (!url.pathname.startsWith('/api/')) {
            await route.continue();
            return;
        }
        const eventIndex = events.findIndex((event) => url.pathname === `/api/v2/events/${event.id}`);
        if (eventIndex >= 0) {
            const target = new URL(`/api/v2/events/${events[1 - eventIndex].id}`, origin);
            await route.fulfill({
                headers: { Link: `<${target}>; rel="${eventIndex === 0 ? 'next' : 'previous'}"` },
                json: events[eventIndex]
            });
            return;
        }
        const responses: Record<string, unknown> = {
            '/api/v2/assistant/access': { enabled: false, has_access: false },
            '/api/v2/organizations': [organization],
            '/api/v2/users/me': {
                email_address: 'synthetic@example.test',
                full_name: 'Synthetic Tester',
                id: '000000000000000000000005',
                is_active: true,
                is_email_address_verified: true,
                organization_ids: [organization.id],
                organization_preferences: [],
                product_tours: { app_welcome: '2026-10-01T12:00:00Z' },
                roles: []
            },
            [`/api/v2/organizations/${organization.id}/projects`]: [project],
            [`/api/v2/organizations/${organization.id}`]: organization,
            [`/api/v2/projects/${project.id}`]: project
        };
        await route.fulfill({ json: responses[url.pathname] ?? (url.pathname.endsWith('/events/count') ? { aggregations: {}, total: 0 } : []) });
    });

    // Act and assert: real line geometry catches wrapping failures that text equality cannot.
    for (const width of [1440, 390]) {
        for (const theme of ['light', 'dark']) {
            await test.step(`${width}px ${theme}: wrap highlighted JSON and preserve its data`, async () => {
                await page.setViewportSize({ height: 1000, width });
                await page.goto(`/event/${events[0].id}`);
                await expect(page.getByRole('button', { exact: true, name: 'View Event JSON' })).toBeVisible();
                await page.evaluate((theme) => {
                    localStorage.setItem('mode-watcher-mode', theme);
                    dispatchEvent(new StorageEvent('storage', { key: 'mode-watcher-mode', newValue: theme, storageArea: localStorage }));
                }, theme);
                if (theme === 'dark') {
                    await expect(page.locator('html')).toHaveClass(/dark/);
                } else {
                    await expect(page.locator('html')).not.toHaveClass(/dark/);
                }
                await page.getByRole('button', { exact: true, name: 'View Event JSON' }).click();
                const dialog = page.getByRole('dialog', { name: 'View Event JSON' });
                await expectJsonLayout(dialog, events[0]);
                await dialog.getByRole('button', { exact: true, name: 'Copy to Clipboard' }).click();
                await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toBe(JSON.stringify(events[0], null, 2));
                expect(JSON.parse(await page.evaluate(() => navigator.clipboard.readText()))).toEqual(events[0]);
                await dialog.getByRole('button', { exact: true, name: 'Close' }).click();
            });
        }
    }

    await test.step('reopen and navigate to JSON stored as a string without changing its type', async () => {
        await page.getByRole('button', { exact: true, name: 'View Event JSON' }).click();
        const dialog = page.getByRole('dialog', { name: 'View Event JSON' });
        await expectJsonLayout(dialog, events[0]);
        await dialog.getByRole('button', { exact: true, name: 'Close' }).click();
        await page.getByRole('button', { exact: true, name: 'Newer event' }).click();
        await expect(page).toHaveURL(new RegExp(`/event/${events[1].id}`));
        await page.getByRole('button', { exact: true, name: 'View Event JSON' }).click();
        await expectJsonLayout(dialog, events[1]);
        await dialog.getByRole('button', { exact: true, name: 'Copy to Clipboard' }).click();
        await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toBe(JSON.stringify(events[1], null, 2));
        expect(JSON.parse(await page.evaluate(() => navigator.clipboard.readText()))).toEqual(events[1]);
    });
});

async function expectJsonLayout(dialog: Locator, event: (typeof events)[number]) {
    const code = dialog.locator('code');
    await expect(code).toBeVisible();
    expect(await code.textContent()).toBe(JSON.stringify(event, null, 2));
    await expect(dialog.locator('img, script, [onerror]')).toHaveCount(0);

    const geometry = await code.evaluate((element) => {
        const pre = element.closest('pre')!;
        const lines = Array.from(element.querySelectorAll('.line'));
        const longLine = lines.find((line) => line.textContent?.includes('unbroken'))!;
        const range = document.createRange();
        range.selectNodeContents(longLine);
        const visualRows = new Set(Array.from(range.getClientRects(), (rect) => Math.round(rect.top)));
        const indents = lines
            .filter((line) => /"(data|Request)":/.test(line.textContent ?? ''))
            .map((line) => {
                const walker = document.createTreeWalker(line, NodeFilter.SHOW_TEXT);
                let node;
                while ((node = walker.nextNode())) {
                    const offset = node.textContent!.search(/\S/);
                    if (offset >= 0) {
                        range.setStart(node, offset);
                        range.setEnd(node, offset + 1);
                        return range.getBoundingClientRect().x;
                    }
                }
                return 0;
            });
        return { clientWidth: pre.clientWidth, indents, scrollWidth: pre.scrollWidth, visualRows: visualRows.size };
    });
    expect(geometry.visualRows).toBeGreaterThan(1);
    expect(geometry.scrollWidth).toBeLessThanOrEqual(geometry.clientWidth + 1);
    expect(geometry.indents[1]).toBeGreaterThan(geometry.indents[0]);
}
