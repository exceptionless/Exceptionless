import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import WS from 'vitest-websocket-mock';

import { WebSocketClient, type WebSocketClientOptions } from './web-socket-client.svelte';

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

let server: WS;

beforeEach(() => {
    server = new WS('ws://localhost:1234/api/v2/push');
});

afterEach(() => {
    WS.clean();
});

function createClient(path?: string, options?: WebSocketClientOptions): WebSocketClient {
    return new WebSocketClient(path, {
        baseUrl: 'ws://localhost:1234',
        reconnectDelay: () => 0,
        ...options
    });
}

describe('WebSocketClient', () => {
    describe('Connection Lifecycle', () => {
        it('should connect successfully', async () => {
            const client = createClient();
            client.connect();
            await server.connected;

            expect(client.readyState).toBe(WebSocket.OPEN);
            client.close();
        });

        it('should set readyState to CONNECTING then OPEN', async () => {
            const client = createClient();
            client.connect();

            expect(client.readyState).toBe(WebSocket.CONNECTING);
            await server.connected;
            expect(client.readyState).toBe(WebSocket.OPEN);

            client.close();
        });

        it('should call onConnecting with isReconnect=false on first connection', async () => {
            const onConnecting = vi.fn();
            const client = createClient();
            client.onConnecting = onConnecting;

            client.connect();
            expect(onConnecting).toHaveBeenCalledWith(false);
            await server.connected;

            client.close();
        });

        it('should call onOpen with isReconnect=false on first connection', async () => {
            const onOpen = vi.fn();
            const client = createClient();
            client.onOpen = onOpen;

            client.connect();
            await server.connected;

            expect(onOpen).toHaveBeenCalledWith(expect.anything(), false);
            client.close();
        });

        it('should handle multiple connect calls gracefully', async () => {
            const client = createClient();

            client.connect();
            client.connect();
            client.connect();

            await server.connected;
            expect(client.readyState).toBe(WebSocket.OPEN);

            client.close();
        });

        it('should use custom connectionTimeout option', async () => {
            const onConnecting = vi.fn();
            const client = new WebSocketClient('/api/v2/push', {
                baseUrl: 'ws://localhost:9999',
                connectionTimeout: 75, // Very short timeout
                reconnectDelay: () => 1000 // Prevent immediate reconnect
            });
            client.onConnecting = onConnecting;

            client.connect();
            expect(client.readyState).toBe(WebSocket.CONNECTING);

            // Wait for custom timeout to expire and close to be triggered
            await new Promise((resolve) => setTimeout(resolve, 150));

            // onConnecting was called with isReconnect=false for initial connect
            expect(onConnecting).toHaveBeenCalledWith(false);
        });
    });

    describe('Disconnection', () => {
        it('should close WebSocket when close() is called', async () => {
            const client = createClient();
            client.connect();
            await server.connected;

            const result = client.close();
            await new Promise((resolve) => setTimeout(resolve, 10));

            expect(result).toBe(true);
            expect(client.readyState).toBe(WebSocket.CLOSED);
        });

        it('should return false when closing already closed connection', () => {
            const client = createClient();
            client.close();
            const result = client.close();

            expect(result).toBe(false);
        });

        it('should call onClose callback', async () => {
            const onClose = vi.fn();
            const client = createClient();
            client.onClose = onClose;

            client.connect();
            await server.connected;

            server.close({ code: 1000, reason: 'Test', wasClean: true });
            await new Promise((resolve) => setTimeout(resolve, 10));

            expect(onClose).toHaveBeenCalledWith(
                expect.objectContaining({
                    code: 1000,
                    reason: 'Test',
                    wasClean: true
                })
            );
        });

        it('should NOT reconnect after manual close', async () => {
            const client = createClient();
            client.connect();
            await server.connected;

            client.close();
            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(client.readyState).toBe(WebSocket.CLOSED);
        });
    });

    describe('Reconnection Logic', () => {
        it('should NOT reconnect on policy violation (code 1008) - auth failure', async () => {
            const client = createClient();
            const onClose = vi.fn();
            client.onClose = onClose;

            client.connect();
            await server.connected;

            server.close({ code: 1008, reason: 'Policy Violation', wasClean: false });
            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(onClose).toHaveBeenCalledWith(
                expect.objectContaining({
                    code: 1008,
                    reason: 'Policy Violation',
                    wasClean: false
                })
            );
            expect(client.readyState).toBe(WebSocket.CLOSED);
        });

        it('should NOT reconnect on abnormal closure (code 1006, wasClean=false) - connection lost unexpectedly', async () => {
            const client = createClient();
            const onClose = vi.fn();
            client.onClose = onClose;

            client.connect();
            await server.connected;

            server.close({ code: 1006, reason: 'Abnormal Closure', wasClean: false });
            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(onClose).toHaveBeenCalledWith(
                expect.objectContaining({
                    code: 1006,
                    reason: 'Abnormal Closure',
                    wasClean: false
                })
            );
            expect(client.readyState).toBe(WebSocket.CLOSED);
        });

        it('should NOT reconnect on unauthorized (code 4401) - 401 HTTP equivalent', async () => {
            const client = createClient();
            const onClose = vi.fn();
            client.onClose = onClose;

            client.connect();
            await server.connected;

            server.close({ code: 4401, reason: 'Unauthorized', wasClean: false });
            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(onClose).toHaveBeenCalledWith(
                expect.objectContaining({
                    code: 4401,
                    reason: 'Unauthorized',
                    wasClean: false
                })
            );
            expect(client.readyState).toBe(WebSocket.CLOSED);
        });

        it('should NOT reconnect on forbidden (code 4403) - 403 HTTP equivalent', async () => {
            const client = createClient();
            const onClose = vi.fn();
            client.onClose = onClose;

            client.connect();
            await server.connected;

            server.close({ code: 4403, reason: 'Forbidden', wasClean: false });
            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(onClose).toHaveBeenCalledWith(
                expect.objectContaining({
                    code: 4403,
                    reason: 'Forbidden',
                    wasClean: false
                })
            );
            expect(client.readyState).toBe(WebSocket.CLOSED);
        });

        it('should reconnect on normal closure (code 1000) - server initiated graceful close', async () => {
            const onConnecting = vi.fn();
            const client = createClient();
            client.onConnecting = onConnecting;

            client.connect();
            await server.connected;
            onConnecting.mockClear();

            server.close({ code: 1000, reason: 'Normal Closure', wasClean: true });
            await new Promise((resolve) => setTimeout(resolve, 10));
            await server.connected;

            expect(onConnecting).toHaveBeenCalledWith(true);
            client.close();
        });

        it('should reconnect on going away (code 1001) - server restart', async () => {
            const onConnecting = vi.fn();
            const client = createClient();
            client.onConnecting = onConnecting;

            client.connect();
            await server.connected;
            onConnecting.mockClear();

            server.close({ code: 1001, reason: 'Going Away', wasClean: true });
            await new Promise((resolve) => setTimeout(resolve, 10));
            await server.connected;

            expect(onConnecting).toHaveBeenCalledWith(true);
            client.close();
        });

        it('should call onConnecting with isReconnect=true on reconnection', async () => {
            const onConnecting = vi.fn();
            const client = createClient();
            client.onConnecting = onConnecting;

            client.connect();
            await server.connected;
            expect(onConnecting).toHaveBeenCalledWith(false);
            onConnecting.mockClear();

            server.close({ code: 1000, reason: 'Test', wasClean: true });
            await new Promise((resolve) => setTimeout(resolve, 10));

            expect(onConnecting).toHaveBeenCalledWith(true);
            await server.connected;
            client.close();
        });
    });

    describe('Message Handling', () => {
        it('should send messages when connected', async () => {
            const client = createClient();
            client.connect();
            await server.connected;

            client.send('test message');
            await expect(server).toReceiveMessage('test message');

            client.close();
        });

        it('should throw error when sending while disconnected', () => {
            const client = createClient();

            expect(() => client.send('test')).toThrow('INVALID_STATE_ERR');
        });

        it('should call onMessage callback when receiving messages', async () => {
            const onMessage = vi.fn();
            const client = createClient();
            client.onMessage = onMessage;

            client.connect();
            await server.connected;

            server.send('test data');
            await new Promise((resolve) => setTimeout(resolve, 10));

            expect(onMessage).toHaveBeenCalledWith(
                expect.objectContaining({
                    data: 'test data'
                })
            );

            client.close();
        });

        it('should receive JSON messages', async () => {
            const onMessage = vi.fn();
            const client = createClient();
            client.onMessage = onMessage;

            client.connect();
            await server.connected;

            const message = JSON.stringify({ data: 'hello', type: 'test' });
            server.send(message);
            await new Promise((resolve) => setTimeout(resolve, 10));

            expect(onMessage).toHaveBeenCalledWith(
                expect.objectContaining({
                    data: message
                })
            );

            client.close();
        });
    });

    describe('Error Handling', () => {
        it('should call onError callback', async () => {
            const onError = vi.fn();
            const client = createClient();
            client.onError = onError;

            client.connect();
            await server.connected;

            server.error();
            await new Promise((resolve) => setTimeout(resolve, 10));

            expect(onError).toHaveBeenCalled();
            client.close();
        });
    });

    describe('URL Construction', () => {
        it('should construct correct WebSocket URL', () => {
            const client = createClient('/api/v2/push');

            expect(client.url).toBe('ws://localhost:1234/api/v2/push');
        });

        it('should use custom base URL', async () => {
            const customClient = new WebSocketClient('/api/v2/push', {
                baseUrl: 'ws://custom-host:5000',
                reconnectDelay: () => 0
            });

            const customServer = new WS('ws://custom-host:5000/api/v2/push');
            customClient.connect();
            await customServer.connected;

            expect(customClient.readyState).toBe(WebSocket.OPEN);

            customClient.close();
            customServer.close();
        });

        it('should handle custom path', async () => {
            const client = createClient('/custom/path');
            const customServer = new WS('ws://localhost:1234/custom/path');

            client.connect();
            await customServer.connected;

            expect(client.readyState).toBe(WebSocket.OPEN);

            client.close();
            customServer.close();
        });
    });

    describe('Options - getReconnectDelay', () => {
        it('should use custom getReconnectDelay from options', async () => {
            const getReconnectDelay = vi.fn(() => 100);
            const client = new WebSocketClient('/api/v2/push', {
                baseUrl: 'ws://localhost:1234',
                reconnectDelay: getReconnectDelay
            });

            client.connect();
            await server.connected;

            server.close({ code: 1000, reason: 'Test', wasClean: true });
            await new Promise((resolve) => setTimeout(resolve, 10));
            await server.connected;

            expect(getReconnectDelay).toHaveBeenCalled();
            client.close();
        });

        it('should use immediate reconnection with getReconnectDelay: () => 0', async () => {
            const onConnecting = vi.fn();
            const client = createClient();
            client.onConnecting = onConnecting;

            client.connect();
            await server.connected;
            onConnecting.mockClear();

            const start = Date.now();
            server.close({ code: 1000, reason: 'Test', wasClean: true });

            // Wait for reconnection attempt
            await new Promise((resolve) => setTimeout(resolve, 50));

            // Verify reconnection happened quickly (within 50ms)
            const elapsed = Date.now() - start;
            expect(onConnecting).toHaveBeenCalledWith(true);
            expect(elapsed).toBeLessThan(100);

            client.close();
        });
    });

    describe('Edge Cases', () => {
        it('should handle rapid connect/disconnect cycles', async () => {
            const client = createClient();

            client.connect();
            client.close();
            client.connect();
            await server.connected;

            expect(client.readyState).toBe(WebSocket.OPEN);
            client.close();
        });

        it('should maintain connection state correctly', async () => {
            const client = createClient();

            expect(client.readyState).toBe(WebSocket.CLOSED);

            client.connect();
            await server.connected;
            expect(client.readyState).toBe(WebSocket.OPEN);

            client.close();
            await new Promise((resolve) => setTimeout(resolve, 10));
            expect(client.readyState).toBe(WebSocket.CLOSED);
        });
    });
});
