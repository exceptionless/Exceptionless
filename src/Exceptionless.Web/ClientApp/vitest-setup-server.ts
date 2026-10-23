// Polyfill WebSocket for Node.js environment using mock-socket
// This is required for vitest-websocket-mock to work properly in server tests
import { WebSocket } from 'mock-socket';
import { vi } from 'vitest';

globalThis.WebSocket = WebSocket as typeof globalThis.WebSocket;

// Keep unit tests independent of local .env values: every declared public variable is unset
// unless a test mocks it.
vi.mock('$app/env/public', async () => {
    const { variables } = await import('./src/env');
    return Object.fromEntries(Object.keys(variables).map((name) => [name, undefined]));
});
