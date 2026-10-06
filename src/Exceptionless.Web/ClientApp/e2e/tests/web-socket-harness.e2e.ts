import { expect, test } from '@playwright/test';
import { createServer } from 'node:http';

import { dispatchWebSocketMessages, installWebSocketTestHarness, isSseCancellation, waitForWebSocketConnection } from '../support/web-socket';

test('the default SSE harness observes a live fetch and forwards server notifications', async ({ page }) => {
    const payload = 'data: {"type":"EventChanged","message":{"id":"live-canary"}}\n\n';
    let requests = 0;
    await page.route('http://localhost/api/v2/push', (route) => {
        requests++;
        return route.fulfill({
            body: payload,
            headers: { 'Content-Type': 'text/event-stream', 'X-Live-Push-Canary': 'live-canary' },
            status: 200
        });
    });
    await page.route('http://localhost/harness', (route) => route.fulfill({ body: '<html></html>', contentType: 'text/html' }));
    await installWebSocketTestHarness(page);
    await page.goto('http://localhost/harness');

    const result = await page.evaluate(async () => {
        const response = await fetch('http://localhost/api/v2/push');
        const canary = response.headers.get('X-Live-Push-Canary');
        if (!canary) {
            await response.body?.cancel();
            return { body: '', canary };
        }
        return { body: await response.text(), canary };
    });

    expect(requests).toBe(1);
    expect(result).toEqual({ body: payload, canary: 'live-canary' });
});

test('suppressed server messages retain a live SSE connection for injected notifications', async ({ page }) => {
    let requests = 0;
    let closed = false;
    const server = createServer((request, response) => {
        const status = new URL(request.url!, 'http://localhost').searchParams.get('status');
        if (status) {
            response.writeHead(Number(status)).end('synthetic push failure');
            return;
        }
        if (request.url !== '/api/v2/push') {
            response.end('<html></html>');
            return;
        }
        requests++;
        response.writeHead(200, { 'Content-Type': 'text/event-stream' });
        response.write('data: {"type":"EventChanged","message":{"id":"server-canary"}}\n\n');
        response.once('close', () => {
            closed = true;
        });
    });
    await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
    try {
        const address = server.address();
        if (!address || typeof address === 'string') {
            throw new Error('Expected a local SSE fixture port');
        }
        await installWebSocketTestHarness(page, { ignoreServerMessages: true });
        await page.goto(`http://127.0.0.1:${address.port}`);
        await page.evaluate(async () => {
            const controller = new AbortController();
            const response = await fetch('/api/v2/push', { signal: controller.signal });
            const trackedWindow = window as Window & { __controller?: AbortController; __notification?: Promise<string> };
            trackedWindow.__controller = controller;
            trackedWindow.__notification = response
                .body!.getReader()
                .read()
                .then(({ value }) => new TextDecoder().decode(value));
        });
        await waitForWebSocketConnection(page);
        expect(requests).toBe(1);

        await dispatchWebSocketMessages(page, [{ message: { id: 'injected-canary' }, type: 'EventChanged' }]);

        const notification = await page.evaluate(() => (window as Window & { __notification?: Promise<string> }).__notification);
        expect(notification).toMatch(/^data: .+\n\n$/);
        expect(JSON.parse(notification!.slice('data: '.length).trim())).toEqual({ message: { id: 'injected-canary' }, type: 'EventChanged' });

        const cancellation = page.waitForEvent('requestfailed', { predicate: (request) => new URL(request.url()).pathname === '/api/v2/push' });
        await page.evaluate(() => (window as Window & { __controller?: AbortController }).__controller!.abort());
        expect(isSseCancellation(await cancellation)).toBe(true);
        await expect.poll(() => closed).toBe(true);

        for (const [pathname, method, errorText] of [
            ['/api/v2/events/canary', 'GET', 'net::ERR_ABORTED'],
            ['/api/v2/organizations/canary/events', 'GET', 'net::ERR_ABORTED'],
            ['/api/v2/organizations/canary/events/count', 'GET', 'net::ERR_ABORTED'],
            ['/api/v2/push', 'POST', 'net::ERR_ABORTED'],
            ['/api/v2/push', 'GET', 'net::ERR_CONNECTION_RESET'],
            ['/api/v2/push', 'GET', null]
        ] as const) {
            expect(
                isSseCancellation({
                    failure: () => (errorText ? { errorText } : null),
                    method: () => method,
                    url: () => `http://localhost${pathname}`
                })
            ).toBe(false);
        }
        const failures = await page.evaluate(async () => {
            const statuses = [];
            for (const status of [401, 429, 500]) {
                const response = await fetch(`/api/v2/push?status=${status}`);
                statuses.push(response.status);
            }
            return statuses;
        });
        expect(failures).toEqual([401, 429, 500]);

        closed = false;
        await page.evaluate(async () => {
            await fetch('/api/v2/push');
        });
        expect(requests).toBe(2);
        await page.reload();
        await expect.poll(() => closed).toBe(true);
    } finally {
        server.closeAllConnections();
        await new Promise<void>((resolve) => server.close(() => resolve()));
    }
});

test('aborting an SSE fetch unblocks the reader and removes its controller', async ({ page }) => {
    await installWebSocketTestHarness(page, { synthetic: true });
    await page.goto('about:blank');

    const abortResult = await page.evaluate(async () => {
        const abortController = new AbortController();
        const response = await fetch('http://localhost/api/v2/push', { signal: abortController.signal });
        const reader = response.body?.getReader();
        if (!reader) {
            throw new Error('Expected the SSE test response to have a body');
        }

        const read = reader.read().then(
            () => ({ status: 'resolved' as const }),
            (error: unknown) => ({
                name: error instanceof DOMException ? error.name : String(error),
                status: 'rejected' as const
            })
        );
        abortController.abort();
        return read;
    });

    expect(abortResult).toEqual({ name: 'AbortError', status: 'rejected' });
    await expect
        .poll(() =>
            page.evaluate(() => {
                const trackedWindow = window as Window & {
                    __exceptionlessE2ESseControllers?: unknown[];
                };
                return trackedWindow.__exceptionlessE2ESseControllers?.length ?? -1;
            })
        )
        .toBe(0);
});

test('connection waits and reconnect counts track the SSE harness', async ({ page }) => {
    await installWebSocketTestHarness(page, { ignoreServerMessages: true, synthetic: true });
    await page.goto('about:blank');

    await page.evaluate(async () => {
        const abortController = new AbortController();
        await fetch('http://localhost/api/v2/push', { signal: abortController.signal });
        abortController.abort();
        await fetch('http://localhost/api/v2/push');
    });
    await waitForWebSocketConnection(page);

    const counts = await page.evaluate(() => {
        const trackedWindow = window as Window & {
            __exceptionlessE2ESseControllers?: unknown[];
            __exceptionlessE2EWebSocketConnections?: number;
        };
        return [trackedWindow.__exceptionlessE2ESseControllers?.length, trackedWindow.__exceptionlessE2EWebSocketConnections];
    });
    expect(counts).toEqual([1, 2]);
});
