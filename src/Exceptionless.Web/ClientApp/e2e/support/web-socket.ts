import type { Page } from '@playwright/test';

interface TrackedWebSocketWindow extends Window {
    __exceptionlessE2EWebSockets?: WebSocket[];
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

export async function installWebSocketTestHarness(page: Page): Promise<void> {
    await page.addInitScript(() => {
        const trackedWindow = window as TrackedWebSocketWindow;
        if (trackedWindow.__exceptionlessE2EWebSockets) {
            return;
        }

        const NativeWebSocket = window.WebSocket;
        const sockets: WebSocket[] = [];

        class TrackedWebSocket extends NativeWebSocket {
            constructor(url: string | URL, protocols?: string | string[]) {
                if (protocols === undefined) {
                    super(url);
                } else {
                    super(url, protocols);
                }

                sockets.push(this);
            }
        }

        trackedWindow.__exceptionlessE2EWebSockets = sockets;
        window.WebSocket = TrackedWebSocket;
    });
}
