import { expect, test } from '../fixtures/e2e-test';

test('transient API failures only open Service Status when the health probe fails', async ({ e2eScenario, page }) => {
    const webSocketErrors: string[] = [];
    const serviceStatusNavigations: string[] = [];
    let healthRequests = 0;

    page.on('console', (message) => {
        if (message.type() === 'error' && message.text().includes('[WebSocketClient]')) {
            webSocketErrors.push(message.text());
        }
    });
    page.on('framenavigated', (frame) => {
        if (frame === page.mainFrame() && new URL(frame.url()).pathname === '/next/status') {
            serviceStatusNavigations.push(frame.url());
        }
    });
    page.on('request', (request) => {
        if (new URL(request.url()).pathname === '/health') {
            healthRequests += 1;
        }
    });

    const targetUrl = '/next/project/list?project=test-project#details';
    let transientFailuresRemaining = 1;
    await page.route('**/api/v2/projects**', async (route) => {
        if (transientFailuresRemaining > 0) {
            transientFailuresRemaining -= 1;
            await route.fulfill({
                body: JSON.stringify({ status: 503, title: 'Controlled transient failure' }),
                contentType: 'application/problem+json',
                status: 503
            });
            return;
        }

        await route.continue();
    });

    await test.step('stay on the current page when the service is healthy', async () => {
        await page.goto(targetUrl);

        await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible();
        await expect(page.getByText(e2eScenario.projectName, { exact: true })).toBeVisible({ timeout: 30_000 });
        await expect.poll(() => healthRequests).toBe(1);
        expect(serviceStatusNavigations).toEqual([]);
        expect(webSocketErrors).toEqual([]);
    });

    await page.unroute('**/api/v2/projects**');
    await page.route('**/api/v2/projects**', async (route) => {
        await route.fulfill({
            body: JSON.stringify({ status: 503, title: 'Controlled service failure' }),
            contentType: 'application/problem+json',
            status: 503
        });
    });
    await page.route('**/health', async (route) => {
        await route.fulfill({ body: 'Unavailable', contentType: 'text/plain', status: 503 });
    });

    await test.step('coalesce the redirect and preserve the current URL when the service is unavailable', async () => {
        await page.reload();
        await expect(page).toHaveURL(/\/next\/status(?:[?#]|$)/);

        const statusUrl = new URL(page.url());
        expect(statusUrl.searchParams.get('redirect')).toBe(targetUrl);
        expect(serviceStatusNavigations).toHaveLength(1);
        expect(webSocketErrors).toEqual([]);
    });
});
