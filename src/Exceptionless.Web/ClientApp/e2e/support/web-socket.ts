import type { Page } from '@playwright/test';

interface TrackedWebSocketWindow extends Window {
    __exceptionlessE2ESseControllers?: ReadableStreamDefaultController<Uint8Array>[];
    __exceptionlessE2EWebSockets?: WebSocket[];
}

export async function dispatchWebSocketMessages(page: Page, messages: unknown[]): Promise<void> {
    await page.evaluate((messages) => {
        const trackedWindow = window as TrackedWebSocketWindow;
        const sockets = trackedWindow.__exceptionlessE2EWebSockets ?? [];
        const socket = sockets.find((candidate) => candidate.readyState === WebSocket.OPEN && candidate.url.includes('/api/v2/push'));
        if (socket) {
            for (const message of messages) {
                socket.dispatchEvent(new MessageEvent('message', { data: JSON.stringify(message) }));
            }
            return;
        }

        const controllers = trackedWindow.__exceptionlessE2ESseControllers ?? [];
        const controller = controllers.at(-1);
        if (!controller) {
            throw new Error('No open Exceptionless push connection was captured');
        }

        const encoder = new TextEncoder();
        for (const message of messages) {
            controller.enqueue(encoder.encode(`data: ${JSON.stringify(message)}\n\n`));
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

        const NativeFetch = window.fetch.bind(window);
        const controllers: ReadableStreamDefaultController<Uint8Array>[] = [];
        trackedWindow.__exceptionlessE2ESseControllers = controllers;
        window.fetch = async (input, init) => {
            const requestUrl = typeof input === 'string' ? input : input instanceof Request ? input.url : String(input);
            const pathname = new URL(requestUrl, window.location.href).pathname;
            if (pathname !== '/api/v2/push') {
                return NativeFetch(input, init);
            }

            let activeController: ReadableStreamDefaultController<Uint8Array> | undefined;
            const stream = new ReadableStream<Uint8Array>({
                cancel() {
                    if (activeController) {
                        const index = controllers.indexOf(activeController);
                        if (index >= 0) {
                            controllers.splice(index, 1);
                        }
                    }
                },
                start(controller) {
                    activeController = controller;
                    controllers.push(controller);
                }
            });

            return new Response(stream, {
                headers: {
                    'Content-Type': 'text/event-stream'
                },
                status: 200
            });
        };
    });
}
