import { expect, test } from '../fixtures/e2e-test';

test.skip(process.env.E2E_ENV === 'production', 'API origin configuration coverage is local only.');

test('configured HTTPS preserves incoming links before starting HTTP API requests', async ({ e2eApi, page }) => {
    const httpOrigin = 'http://localhost:65532';
    const httpsOrigin = 'https://localhost:65532';
    const destination = '/next/login?redirect=%2Fstack%2Fall#notice';
    let sslFlagInjected = false;
    const insecureApiRequests: string[] = [];
    page.on('request', (request) => {
        const url = new URL(request.url());
        if (url.protocol === 'http:' && url.pathname.startsWith('/api/')) {
            insecureApiRequests.push(url.pathname);
        }
    });

    // Model a proxy exposing HTTP and HTTPS while serving the real local Svelte app.
    await page.route(`${httpOrigin}/**`, async (route) => {
        const url = new URL(route.request().url());
        const response = await route.fetch({ url: new URL(url.pathname + url.search, e2eApi.environment.appUrl).href });
        if (url.pathname.endsWith('virtual:env/dynamic/public')) {
            sslFlagInjected = true;
            await route.fulfill({ body: `${await response.text()}\nenv.PUBLIC_ENABLE_SSL = 'true';`, response });
        } else {
            await route.fulfill({ response });
        }
    });
    await page.route(`${httpsOrigin}/**`, (route) => route.fulfill({ body: '<h1>Secure destination</h1>', contentType: 'text/html' }));

    await page.goto(`${httpOrigin}${destination}`, { waitUntil: 'domcontentloaded' });
    await expect(page).toHaveURL(`${httpsOrigin}${destination}`);
    await expect(page.getByRole('heading', { name: 'Secure destination' })).toBeVisible();
    expect(sslFlagInjected).toBe(true);
    expect(insecureApiRequests).toEqual([]);
});

test('the OAuth bridge opens the application login and preserves the authorization return URL', async ({ e2eApi, page, request }) => {
    const query = '?client_id=local-client&state=local-cutover-check';
    const response = await request.get(`${e2eApi.environment.apiUrl}/oauth/authorize${query}`, { maxRedirects: 0 });
    expect(response.status()).toBe(302);
    const destination = new URL(response.headers().location);
    expect(destination.pathname).toBe('/oauth/authorize');
    expect(destination.search).toBe(query);
    expect(destination.hash).toBe('');
    expect(destination.port).toBe(new URL(e2eApi.environment.appUrl).port);

    await page.goto(destination.href);
    await expect(page).toHaveURL((url) => url.pathname === '/login' && url.searchParams.get('redirect') === `/oauth/authorize${query}`);
    await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
});

test('status waits for the configured API to recover before returning to the application', async ({ page }) => {
    const apiOrigin = 'https://localhost:65533';
    await page.addInitScript((origin) => localStorage.setItem('PUBLIC_BASE_URL', origin), apiOrigin);
    let apiHealthy = false;
    let apiHealthRequests = 0;
    let sameOriginHealthRequests = 0;
    await page.route('**/health', async (route) => {
        if (new URL(route.request().url()).origin === apiOrigin) {
            apiHealthRequests++;
            await route.fulfill({ body: apiHealthy ? 'Healthy' : 'Unhealthy', status: apiHealthy ? 200 : 503 });
        } else {
            sameOriginHealthRequests++;
            await route.fulfill({ body: 'Healthy' });
        }
    });

    await page.goto('/status?redirect=%2Flogin');
    await expect.poll(() => apiHealthRequests).toBeGreaterThan(0);
    await expect(page).toHaveURL(/\/status\?redirect=/);
    await expect(page.getByText('Service Status', { exact: true })).toBeVisible();
    expect(sameOriginHealthRequests).toBe(0);

    apiHealthy = true;
    await expect(page).toHaveURL(/\/login$/, { timeout: 40_000 });
    expect(apiHealthRequests).toBeGreaterThan(1);
    expect(sameOriginHealthRequests).toBe(0);
});

test('the application sends API requests and push connections to its configured origin', async ({ e2eApi, e2eScenario, page }) => {
    const apiOrigin = 'https://localhost:65533';
    const telemetryOrigin = 'https://localhost:65534';
    await page.addInitScript(
        ({ apiOrigin, telemetryOrigin }) => {
            localStorage.setItem('PUBLIC_BASE_URL', apiOrigin);
            localStorage.setItem('PUBLIC_EXCEPTIONLESS_SERVER_URL', telemetryOrigin);
            localStorage.setItem('PUBLIC_EXCEPTIONLESS_API_KEY', '00000000000000000000000000000000');
        },
        { apiOrigin, telemetryOrigin }
    );
    // Telemetry has a separate destination and must not affect the application API-origin assertion.
    const telemetryRequests: string[] = [];
    await page.route(`${telemetryOrigin}/api/v2/**`, async (route) => {
        telemetryRequests.push(new URL(route.request().url()).pathname);
        await route.fulfill({ json: { settings: {}, version: 1 } });
    });
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
    await expect.poll(() => telemetryRequests).toContain('/api/v2/projects/config');
    expect(sameOriginApiRequests).toEqual([]);
});
