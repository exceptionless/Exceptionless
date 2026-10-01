import { expect, test } from '../fixtures/e2e-test';

test.skip(process.env.E2E_ENV === 'production', 'API origin configuration coverage is local only.');

test('the application sends API requests and push connections to its configured origin', async ({ e2eApi, e2eScenario, page }) => {
    const apiOrigin = 'https://localhost:65533';
    await page.addInitScript((origin) => localStorage.setItem('PUBLIC_BASE_URL', origin), apiOrigin);
    // Intercept a distinct local origin and forward HTTP requests to this test's actual API.
    await page.route(`${apiOrigin}/api/v2/**`, async (route) => {
        const url = new URL(route.request().url());
        const response = await route.fetch({ url: new URL(url.pathname + url.search, e2eApi.environment.appUrl).href });
        await route.fulfill({ response });
    });
    const pushConnections: string[] = [];
    await page.routeWebSocket('wss://localhost:65533/api/v2/push*', (socket) => {
        pushConnections.push(new URL(socket.url()).pathname);
    });
    const sameOriginApiRequests: string[] = [];
    page.on('request', (request) => {
        const url = new URL(request.url());
        if (url.origin === new URL(e2eApi.environment.appUrl).origin && url.pathname.startsWith('/api/v2/')) {
            sameOriginApiRequests.push(url.pathname);
        }
    });

    await page.goto('/stack/all');
    await expect(page.getByRole('heading', { name: 'All' })).toBeVisible();
    await expect(page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first()).toBeVisible();
    await expect.poll(() => pushConnections.length).toBeGreaterThan(0);
    expect(sameOriginApiRequests).toEqual([]);
});
