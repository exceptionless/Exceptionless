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
        const original = new Response('<script>start()</script>', { headers: { 'content-type': 'text/html' } });

        const response = await handle({ event: {} as Parameters<Handle>[0]['event'], resolve: async () => original });
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

        const response = await handle({ event: {} as Parameters<Handle>[0]['event'], resolve: async () => original });

        expect(response).toBe(original);
        expect(response.headers.has('content-security-policy')).toBe(false);
    });

    it('serves HTML with a restrictive policy when the public URL uses the empty same-origin default', async () => {
        publicEnvironment.PUBLIC_BASE_URL = '';
        const original = new Response('<script>start()</script>', { headers: { 'content-type': 'text/html' } });

        const response = await handle({ event: {} as Parameters<Handle>[0]['event'], resolve: async () => original });
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

        await expect(handle({ event: {} as Parameters<Handle>[0]['event'], resolve: async () => original })).rejects.toThrow();
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
