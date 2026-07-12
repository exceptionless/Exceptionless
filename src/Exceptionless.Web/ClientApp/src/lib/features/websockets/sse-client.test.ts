import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { accessToken } from '../auth/index.svelte';
import { SSE_CLOSED, SSE_CONNECTING, SSE_OPEN, SseClient, type SseClientOptions } from './sse-client.svelte';

// Mock the auth module
vi.mock('../auth/index.svelte', () => ({
    accessToken: {
        current: 'test-token-123'
    }
}));

// Mock DocumentVisibility to always return visible
vi.mock('$shared/document-visibility.svelte', () => {
    return {
        DocumentVisibility: class {
            visible = true;
        }
    };
});

function createClient(options?: SseClientOptions): SseClient {
    return new SseClient('/api/v2/push', {
        baseUrl: 'http://localhost:5200',
        reconnectDelay: () => 50,
        ...options
    });
}

// Creates a response whose stream stays open indefinitely (for testing open connections)
function createOpenSseResponse(initialEvents: string[] = []) {
    return new Response(
        new ReadableStream({
            start(controller) {
                for (const event of initialEvents) {
                    controller.enqueue(new TextEncoder().encode(event));
                }
                // intentionally never close
            }
        }),
        {
            headers: { 'Content-Type': 'text/event-stream' },
            status: 200
        }
    );
}

// Helper to create a mock fetch response that streams SSE data
function createSseResponse(events: string[] = [], options: { delay?: number; status?: number } = {}) {
    const { delay = 0, status = 200 } = options;

    return new Response(
        new ReadableStream({
            async start(controller) {
                for (const event of events) {
                    if (delay > 0) {
                        await new Promise((resolve) => setTimeout(resolve, delay));
                    }

                    controller.enqueue(new TextEncoder().encode(event));
                }

                controller.close();
            }
        }),
        {
            headers: { 'Content-Type': 'text/event-stream' },
            status
        }
    );
}

describe('SseClient', () => {
    let fetchMock: ReturnType<typeof vi.fn<typeof fetch>>;
    let activeClients: SseClient[] = [];

    beforeEach(() => {
        accessToken.current = 'test-token-123';
        fetchMock = vi.fn<typeof fetch>();
        global.fetch = fetchMock as typeof fetch;
    });

    afterEach(() => {
        for (const client of activeClients) {
            client.close();
        }

        activeClients = [];
        vi.restoreAllMocks();
    });

    function trackedClient(options?: SseClientOptions): SseClient {
        const client = createClient(options);
        activeClients.push(client);
        return client;
    }

    describe('Connection Lifecycle', () => {
        it('should connect successfully and call onOpen', async () => {
            const onOpen = vi.fn();
            fetchMock.mockResolvedValue(createOpenSseResponse([': keepalive\n\n']));

            const client = trackedClient();
            client.onOpen = onOpen;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(fetchMock).toHaveBeenCalledWith(
                'http://localhost:5200/api/v2/push',
                expect.objectContaining({
                    headers: expect.objectContaining({
                        Accept: 'text/event-stream',
                        Authorization: 'Bearer test-token-123'
                    })
                })
            );
            expect(onOpen).toHaveBeenCalledWith(false);

            client.close();
        });

        it('should set readyState to CONNECTING then OPEN', async () => {
            fetchMock.mockResolvedValue(createOpenSseResponse([': keepalive\n\n']));

            const client = trackedClient();
            client.connect();

            expect(client.readyState).toBe(SSE_CONNECTING);

            await new Promise((resolve) => setTimeout(resolve, 50));
            expect(client.readyState).toBe(SSE_OPEN);

            client.close();
        });

        it('should call onConnecting with isReconnect=false on first connection', async () => {
            const onConnecting = vi.fn();
            fetchMock.mockResolvedValue(createSseResponse([]));

            const client = trackedClient();
            client.onConnecting = onConnecting;
            client.connect();

            expect(onConnecting).toHaveBeenCalledWith(false);
            client.close();
        });
    });

    describe('Disconnection', () => {
        it('should close when close() is called', async () => {
            // Create a response that never closes
            fetchMock.mockResolvedValue(
                new Response(
                    new ReadableStream({
                        start() {
                            // Never close - simulate long-lived connection
                        }
                    }),
                    { headers: { 'Content-Type': 'text/event-stream' }, status: 200 }
                )
            );

            const client = trackedClient();
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 50));
            const result = client.close();

            expect(result).toBe(true);
            expect(client.readyState).toBe(SSE_CLOSED);
        });

        it('should return false when closing already closed connection', () => {
            const client = trackedClient();
            const result = client.close();

            expect(result).toBe(false);
        });

        it('should NOT reconnect after manual close', async () => {
            // Use a stream that stays open (never closes) so we can test manual close
            fetchMock.mockResolvedValue(
                new Response(
                    new ReadableStream({
                        start() {
                            // intentionally never close - stream stays open
                        }
                    }),
                    { headers: { 'Content-Type': 'text/event-stream' }, status: 200 }
                )
            );

            const client = trackedClient();
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 50));
            client.close();

            await new Promise((resolve) => setTimeout(resolve, 100));
            expect(client.readyState).toBe(SSE_CLOSED);
            // fetch should only be called once (no reconnect)
            expect(fetchMock).toHaveBeenCalledTimes(1);
        });

        it('should allow reconnect after internal close', async () => {
            fetchMock.mockImplementation(() => Promise.resolve(createOpenSseResponse([': keepalive\n\n'])));

            const client = trackedClient();
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 50));
            expect(client.close(false)).toBe(true);

            client.connect();
            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(client.readyState).toBe(SSE_OPEN);
            expect(fetchMock).toHaveBeenCalledTimes(2);
        });
    });

    describe('Auth Failure Handling', () => {
        it('should NOT reconnect on 401', async () => {
            fetchMock.mockImplementation(() => Promise.resolve(new Response(null, { status: 401 })));

            const onClose = vi.fn();
            const client = trackedClient();
            client.onClose = onClose;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));

            expect(onClose).toHaveBeenCalledTimes(1);
            expect(client.readyState).toBe(SSE_CLOSED);
            expect(fetchMock).toHaveBeenCalledTimes(1);
            expect(accessToken.current).toBe('');
        });

        it('should NOT reconnect on 403', async () => {
            fetchMock.mockImplementation(() => Promise.resolve(new Response(null, { status: 403 })));

            const client = trackedClient();
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));

            expect(client.readyState).toBe(SSE_CLOSED);
            expect(fetchMock).toHaveBeenCalledTimes(1);
        });

        it('should slowly probe again when push endpoint is unavailable', async () => {
            const onClose = vi.fn();
            fetchMock.mockImplementation(() => Promise.resolve(new Response(null, { status: 404 })));

            const client = trackedClient({ unavailableRetryDelay: 25 });
            client.onClose = onClose;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));

            expect(onClose).toHaveBeenCalled();
            expect(fetchMock.mock.calls.length).toBeGreaterThan(1);
        });
    });

    describe('Reconnection Logic', () => {
        it('should reconnect when stream ends normally', async () => {
            let callCount = 0;
            fetchMock.mockImplementation(() => {
                callCount++;
                return Promise.resolve(createSseResponse([': keepalive\n\n']));
            });

            const client = trackedClient({ baseUrl: 'http://localhost:5200', reconnectDelay: () => 10 });
            client.connect();

            // Wait for initial connection + stream end + reconnect
            await new Promise((resolve) => setTimeout(resolve, 200));

            expect(callCount).toBeGreaterThan(1);
            client.close();
        });

        it('should use custom reconnectDelay', async () => {
            const reconnectDelay = vi.fn(() => 50);
            fetchMock.mockResolvedValue(createSseResponse([]));

            const client = trackedClient({ baseUrl: 'http://localhost:5200', reconnectDelay });
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 150));

            expect(reconnectDelay).toHaveBeenCalled();
            client.close();
        });
    });

    describe('Message Handling', () => {
        it('should parse SSE data messages and call onMessage', async () => {
            const onMessage = vi.fn();
            const sseData = 'data: {"type":"StackChanged","message":{"id":"123"}}\n\n';
            fetchMock.mockResolvedValue(createSseResponse([sseData]));

            const client = trackedClient();
            client.onMessage = onMessage;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));

            expect(onMessage).toHaveBeenCalledWith(
                expect.objectContaining({
                    data: '{"type":"StackChanged","message":{"id":"123"}}'
                })
            );
            client.close();
        });

        it('should ignore keep-alive comments', async () => {
            const onMessage = vi.fn();
            const sseData = ': keepalive\n\ndata: {"type":"test","message":{}}\n\n';
            fetchMock.mockResolvedValue(createSseResponse([sseData]));

            const client = trackedClient();
            client.onMessage = onMessage;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));

            // Should only get the data message, not the keepalive
            expect(onMessage).toHaveBeenCalledTimes(1);
            expect(onMessage).toHaveBeenCalledWith(
                expect.objectContaining({
                    data: '{"type":"test","message":{}}'
                })
            );
            client.close();
        });

        it('should handle multiple messages in one chunk', async () => {
            const onMessage = vi.fn();
            const sseData = 'data: {"type":"A","message":{}}\n\ndata: {"type":"B","message":{}}\n\n';
            fetchMock.mockResolvedValue(createSseResponse([sseData]));

            const client = trackedClient();
            client.onMessage = onMessage;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));

            expect(onMessage).toHaveBeenCalledTimes(2);
            client.close();
        });

        it('should handle messages split across chunks', async () => {
            const onMessage = vi.fn();
            fetchMock.mockResolvedValue(createSseResponse(['data: {"type":"Sp', 'lit","message":{}}\n\n']));

            const client = trackedClient();
            client.onMessage = onMessage;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));

            expect(onMessage).toHaveBeenCalledWith(
                expect.objectContaining({
                    data: '{"type":"Split","message":{}}'
                })
            );
            client.close();
        });

        it('should parse CRLF-delimited and multiline SSE data', async () => {
            const onMessage = vi.fn();
            fetchMock.mockResolvedValue(createSseResponse(['data: first\r\ndata: second\r\n\r\n']));

            const client = trackedClient();
            client.onMessage = onMessage;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 100));
            expect(onMessage).toHaveBeenCalledWith(expect.objectContaining({ data: 'first\nsecond' }));
        });

        it('should reject a successful response with a non-SSE content type', async () => {
            const onError = vi.fn();
            fetchMock.mockResolvedValue(new Response('<html></html>', { headers: { 'Content-Type': 'text/html' }, status: 200 }));

            const client = trackedClient({ reconnectDelay: () => 1000 });
            client.onError = onError;
            client.connect();

            await new Promise((resolve) => setTimeout(resolve, 50));
            expect(onError).toHaveBeenCalledWith(expect.objectContaining({ message: expect.stringContaining('invalid content type') }));
        });
    });

    describe('URL Construction', () => {
        it('should construct correct SSE URL with base URL', () => {
            const client = trackedClient({ baseUrl: 'http://localhost:5200' });
            expect(client.url).toBe('http://localhost:5200/api/v2/push');
        });
    });
});
