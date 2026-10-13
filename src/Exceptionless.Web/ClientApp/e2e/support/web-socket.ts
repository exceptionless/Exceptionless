import { expect, type Page } from '@playwright/test';

interface TrackedWebSocketWindow extends Window {
    __exceptionlessE2EWebSocketConnections?: number;
    __exceptionlessE2EWebSockets?: WebSocket[];
}

export async function churnDocumentVisibility(page: Page): Promise<number> {
    await waitForWebSocketConnection(page);
    const reconnectCount = () => page.evaluate(() => (window as TrackedWebSocketWindow).__exceptionlessE2EWebSocketConnections ?? 0);
    const before = await reconnectCount();
    for (let index = 0; index < 30; index++) {
        await setDocumentHidden(page, true);
        await setDocumentHidden(page, false);
    }

    await waitForWebSocketConnection(page);
    await expect.poll(reconnectCount).toBeGreaterThan(before);
    // Observe late work as well as the immediate resume. Each completed reconnect
    // legitimately refreshes active queries; its count depends on handshake timing.
    await page.waitForTimeout(2_000);
    const reconnects = (await reconnectCount()) - before;
    expect(reconnects).toBeLessThanOrEqual(30);
    return reconnects;
}

export async function dispatchWebSocketMessages(page: Page, messages: unknown[]): Promise<void> {
    await page.evaluate((messages) => {
        const sockets = (window as TrackedWebSocketWindow).__exceptionlessE2EWebSockets ?? [];
        const socket = sockets.find((candidate) => candidate.readyState === WebSocket.OPEN && candidate.url.includes('/api/v2/push'));
        if (!socket) {
            throw new Error('No open Exceptionless WebSocket was captured');
        }

        for (const message of messages) {
            socket.dispatchEvent(new MessageEvent('message', { data: JSON.stringify(message) }));
        }
    }, messages);
}

export async function installWebSocketTestHarness(page: Page, options: { ignoreServerMessages?: boolean } = {}): Promise<void> {
    if (options.ignoreServerMessages) {
        // Keep real connection/reconnection behavior, but let request-budget tests
        // inject their own notifications without late background-job broadcasts.
        await page.routeWebSocket(
            (url) => url.pathname === '/api/v2/push',
            (socket) => {
                const server = socket.connectToServer();
                server.onMessage(() => {});
            }
        );
    }

    await page.addInitScript(() => {
        const trackedWindow = window as TrackedWebSocketWindow;
        if (trackedWindow.__exceptionlessE2EWebSockets) {
            return;
        }

        const NativeWebSocket = window.WebSocket;
        const sockets: WebSocket[] = [];
        trackedWindow.__exceptionlessE2EWebSocketConnections = 0;

        class TrackedWebSocket extends NativeWebSocket {
            constructor(url: string | URL, protocols?: string | string[]) {
                if (protocols === undefined) {
                    super(url);
                } else {
                    super(url, protocols);
                }

                sockets.push(this);
                if (new URL(this.url).pathname === '/api/v2/push') {
                    this.addEventListener('open', () => {
                        if (this.readyState === WebSocket.OPEN) {
                            trackedWindow.__exceptionlessE2EWebSocketConnections!++;
                        }
                    });
                }
            }
        }

        trackedWindow.__exceptionlessE2EWebSockets = sockets;
        window.WebSocket = TrackedWebSocket;
    });
}

export async function setDocumentHidden(page: Page, hidden: boolean): Promise<void> {
    await page.evaluate((nextHidden) => {
        Object.defineProperty(document, 'hidden', { configurable: true, get: () => nextHidden });
        Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => (nextHidden ? 'hidden' : 'visible') });
        document.dispatchEvent(new Event('visibilitychange'));
        window.dispatchEvent(new Event('visibilitychange'));
    }, hidden);
}

export async function waitForWebSocketConnection(page: Page): Promise<void> {
    await page.waitForFunction(() => {
        const sockets = ((window as TrackedWebSocketWindow).__exceptionlessE2EWebSockets ?? []).filter((socket) => socket.url.includes('/api/v2/push'));
        return (
            sockets.filter((socket) => socket.readyState === WebSocket.OPEN).length === 1 &&
            sockets.every((socket) => socket.readyState !== WebSocket.CONNECTING)
        );
    });
}
