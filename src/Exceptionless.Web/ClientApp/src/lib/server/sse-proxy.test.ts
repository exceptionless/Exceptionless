import type { IncomingMessage, ServerResponse } from 'node:http';

import { EventEmitter } from 'node:events';
import { createServer } from 'node:http';
import { afterEach, expect, it } from 'vitest';

import { createSseProxy } from './sse-proxy';

const upstream = createServer();

afterEach(async () => {
    upstream.closeAllConnections();
    await new Promise<void>((resolve) => upstream.close(() => resolve()));
    upstream.removeAllListeners();
});

it('cancels the upstream SSE stream when the downstream HTTP/2 response closes', async () => {
    const closed = Promise.withResolvers<void>();
    upstream.on('request', (_request, response) => {
        response.writeHead(200, { 'Content-Type': 'text/event-stream' });
        response.write(': canary\n\n');
        response.once('close', () => closed.resolve());
    });
    await new Promise<void>((resolve) => upstream.listen(0, '127.0.0.1', resolve));
    const address = upstream.address();
    if (!address || typeof address === 'string') {
        throw new Error('Expected a local upstream port');
    }
    const target = `http://127.0.0.1:${address.port}`;
    const downstream = Object.assign(new EventEmitter(), { destroyed: false, writableFinished: false });
    const options: RequestInit = {};
    const proxy = createSseProxy(target);
    await proxy.fetchOptions!.onBeforeRequest!(options, {} as IncomingMessage, downstream as ServerResponse, {});
    const response = await fetch(`${target}/api/v2/push`, options);
    const reader = response.body!.getReader();
    expect(new TextDecoder().decode((await reader.read()).value)).toBe(': canary\n\n');

    // HTTP/2 marks a reset response as finished before emitting close.
    downstream.writableFinished = true;
    downstream.destroyed = true;
    downstream.emit('close');

    await expect(reader.read()).rejects.toMatchObject({ name: 'AbortError' });
    await closed.promise;
});
