import type { Handle } from '@sveltejs/kit';

import { beforeEach, describe, expect, it, vi } from 'vitest';

const environment = vi.hoisted(() => ({ building: false, dev: false }));
const publicEnvironment = vi.hoisted(() => ({ PUBLIC_BASE_URL: 'https://app.example.test/next' }));
vi.mock('$app/environment', () => environment);
vi.mock('$env/dynamic/public', () => ({ env: publicEnvironment }));

import { handle } from './hooks.server';

describe('server CSP hook', () => {
    beforeEach(() => {
        environment.building = false;
        environment.dev = false;
        publicEnvironment.PUBLIC_BASE_URL = 'https://app.example.test/next';
    });

    it.each([false, true])('limits scheme-wide WebSockets to development (dev=%s)', async (dev) => {
        environment.dev = dev;
        const original = createFrameworkResponse();

        const response = await handle({ event: createEvent(), resolve: async () => original });
        const policy = response.headers.get('content-security-policy')!;
        const connections = policy
            .split('; ')
            .find((directive) => directive.startsWith('connect-src '))!
            .split(' ');

        expect(connections.includes('ws:')).toBe(dev);
        expect(connections.includes('wss:')).toBe(dev);
        expect(connections).toContain('wss://*.intercom-messenger.com');
        expect(connections).toContain('wss://app.example.test');
        expect(policy).toContain("'strict-dynamic'");
        expect(await response.text()).toMatch(/<script nonce="[A-Za-z\d+/]{43}="/);
    });

    it('leaves static build HTML for the ASP.NET response nonce', async () => {
        environment.building = true;
        const original = new Response('<script>start()</script>', { headers: { 'content-type': 'text/html' } });

        const response = await handle({ event: createEvent(), resolve: async () => original });

        expect(response).toBe(original);
        expect(response.headers.has('content-security-policy')).toBe(false);
    });

    it('serves HTML with a restrictive policy when the public URL uses the empty same-origin default', async () => {
        publicEnvironment.PUBLIC_BASE_URL = '';
        const original = new Response('<script>start()</script>', { headers: { 'content-type': 'text/html' } });

        const response = await handle({ event: createEvent(), resolve: async () => original });
        const connections = response.headers
            .get('content-security-policy')!
            .split('; ')
            .find((directive) => directive.startsWith('connect-src '))!
            .split(' ');

        expect(response.status).toBe(200);
        expect(connections).toContain("'self'");
        expect(connections).not.toContain('ws:');
        expect(connections).not.toContain('wss:');
        expect(connections).not.toContain('wss://app.example.test');
    });

    it('rejects a supplied non-HTTP public URL instead of broadening production sources', async () => {
        publicEnvironment.PUBLIC_BASE_URL = 'ftp://app.example.test';
        const original = new Response('<script>start()</script>', { headers: { 'content-type': 'text/html' } });

        await expect(handle({ event: createEvent(), resolve: async () => original })).rejects.toThrow();
    });

    it.each(['GET', 'HEAD'])('removes conditional and range headers before resolving HTML (%s)', async (method) => {
        const event = createEvent(method, '/next/', {
            'if-modified-since': 'Wed, 30 Sep 2026 00:00:00 GMT',
            'if-none-match': '*',
            'if-range': 'old',
            range: 'bytes=0-23'
        });
        const response = await handle({
            event,
            resolve: async (resolvedEvent) => {
                for (const header of ['range', 'if-range', 'if-none-match', 'if-modified-since']) {
                    expect(resolvedEvent.request.headers.has(header)).toBe(false);
                }
                return method === 'HEAD' ? new Response(null, { headers: { 'content-type': 'text/html' } }) : createFrameworkResponse();
            }
        });

        expect(response.status).toBe(200);
        expect(response.headers.get('cache-control')).toBe('no-store');
        if (method === 'HEAD') expect(response.body).toBeNull();
    });

    it.each([
        ['GET', '/next/app.js'],
        ['POST', '/next/']
    ])('preserves conditional headers outside document requests (%s %s)', async (method, path) => {
        const event = createEvent(method, path, { 'if-none-match': 'current' });
        await handle({
            event,
            resolve: async (resolvedEvent) => {
                expect(resolvedEvent.request.headers.get('if-none-match')).toBe('current');
                return Response.json({ ok: true });
            }
        });
    });

    it('does not trust request or forwarded hosts for the production WebSocket origin', async () => {
        const original = new Response('<script>start()</script>', { headers: { 'content-type': 'text/html' } });
        const event = {
            request: new Request('https://untrusted.example/next', { headers: { 'x-forwarded-host': 'forwarded.example' } }),
            url: new URL('https://untrusted.example/next')
        } as Parameters<Handle>[0]['event'];

        const response = await handle({ event, resolve: async () => original });
        const policy = response.headers.get('content-security-policy')!;

        expect(policy).toContain('wss://app.example.test');
        expect(policy).not.toContain('untrusted.example');
        expect(policy).not.toContain('forwarded.example');
    });
});

function createEvent(method = 'GET', path = '/next/', headers: HeadersInit = {}) {
    const url = new URL(path, 'https://app.example.test');
    return { request: new Request(url, { headers, method }), url } as Parameters<Handle>[0]['event'];
}

function createFrameworkResponse() {
    const nonce = 'dGVzdC1mcmFtZXdvcmstbm9uY2U=';
    return new Response(`<script nonce="${nonce}">start()</script>`, {
        headers: { 'content-security-policy': `script-src 'nonce-${nonce}' 'strict-dynamic'`, 'content-type': 'text/html' }
    });
}
