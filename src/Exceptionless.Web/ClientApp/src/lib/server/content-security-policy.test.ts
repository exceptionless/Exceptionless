import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

import { addNonceToScripts, createContentSecurityPolicy, createNonce, secureHtmlResponse } from './content-security-policy';

describe('createNonce', () => {
    it('creates unique base64-encoded 32-byte nonces', () => {
        const nonces = Array.from({ length: 32 }, () => createNonce());

        expect(new Set(nonces)).toHaveLength(nonces.length);
        for (const nonce of nonces) {
            expect(nonce).toMatch(/^[A-Za-z\d+/]{43}=$/);
            expect(Buffer.from(nonce, 'base64')).toHaveLength(32);
        }
    });
});

describe('addNonceToScripts', () => {
    it('adds the nonce to every script opening tag', () => {
        const nonce = createNonce();
        const html = '<script>first()</script><script async src="/second.js"></script>';

        expect(addNonceToScripts(html, nonce)).toBe(`<script nonce="${nonce}">first()</script><script nonce="${nonce}" async src="/second.js"></script>`);
    });

    it('replaces existing quoted, unquoted, and boolean nonce attributes', () => {
        const nonce = createNonce();
        const html = `<script nonce="old"></script><SCRIPT type="module" NONCE='older'></SCRIPT><script nonce defer></script>`;

        expect(addNonceToScripts(html, nonce)).toBe(
            `<script nonce="${nonce}"></script><SCRIPT nonce="${nonce}" type="module"></SCRIPT><script nonce="${nonce}" defer></script>`
        );
    });

    it('preserves script-like text inside inline scripts', () => {
        const nonce = createNonce();
        const html = '<script>const marker = "<script>";</script>';

        expect(addNonceToScripts(html, nonce)).toBe(`<script nonce="${nonce}">const marker = "<script>";</script>`);
    });
});

describe('createContentSecurityPolicy', () => {
    it('matches the canonical cross-runtime policy contract', () => {
        const nonce = createNonce();
        const policy = normalizeDevelopmentPolicy(createContentSecurityPolicy(nonce, { allowDevelopmentConnections: true }));

        expect(policy).toEqual(readPolicyContract());
    });

    it('uses a strict nonce policy with compatibility sources', () => {
        const nonce = createNonce();
        const policy = createContentSecurityPolicy(nonce);
        const scriptDirective = getDirective(policy, 'script-src');
        const connectDirective = getDirective(policy, 'connect-src');

        expect(scriptDirective).toContain(`'nonce-${nonce}'`);
        expect(scriptDirective).toContain("'strict-dynamic'");
        expect(scriptDirective).toContain("'self'");
        expect(scriptDirective).toContain('https://js.stripe.com');
        expect(scriptDirective).toContain('https://*.js.stripe.com');
        expect(scriptDirective).toContain('https://widget.intercom.io');
        expect(scriptDirective).not.toContain("'unsafe-inline'");
        expect(scriptDirective).not.toContain("'unsafe-eval'");
        expect(scriptDirective).not.toContain('https://cdn.jsdelivr.net');

        expect(connectDirective).toContain("'self'");
        expect(connectDirective).toContain('https://api.stripe.com');
        expect(connectDirective).toContain('wss://*.intercom-messenger.com');
        expect(connectDirective).not.toContain('ws:');
        expect(connectDirective).not.toContain('wss:');

        expect(getDirective(policy, 'img-src')).not.toContain('http://www.gravatar.com');
        expect(policy).not.toContain('intercomcdn.eu');
        expect(policy).not.toContain('.eu.intercom.io');
        expect(policy).not.toContain('.au.intercom.io');
        expect(policy).not.toContain('au.intercomcdn.com');
        expect(policy).not.toContain('static.au.intercomassets.com');
        expect(policy).not.toContain('intercom-attachments.eu');
        expect(policy).not.toContain('au.intercom-attachments.com');

        expect(getDirective(policy, 'base-uri')).toEqual(["'none'"]);
        expect(getDirective(policy, 'object-src')).toEqual(["'none'"]);
        expect(getDirective(policy, 'frame-ancestors')).toEqual(["'none'"]);
    });

    it('allows broad WebSocket schemes only when development connections are requested', () => {
        const policy = createContentSecurityPolicy(createNonce(), { allowDevelopmentConnections: true });
        const connectDirective = getDirective(policy, 'connect-src');

        expect(connectDirective).toContain('ws:');
        expect(connectDirective).toContain('wss:');
    });
});

describe('secureHtmlResponse', () => {
    it('buffers chunked HTML, nonces every script, and prevents nonce/body caching', async () => {
        const encoder = new TextEncoder();
        const stream = new ReadableStream<Uint8Array>({
            start(controller) {
                controller.enqueue(encoder.encode('<!doctype html><html><body><scr'));
                controller.enqueue(encoder.encode('ipt type="module">start()</script><script src="/app.js"></script></body></html>'));
                controller.close();
            }
        });
        const originalResponse = new Response(stream, {
            headers: {
                'content-length': '123',
                'content-type': 'text/html; charset=utf-8',
                etag: 'stale-after-transformation'
            }
        });

        const response = await secureHtmlResponse(originalResponse, { allowDevelopmentConnections: true });
        const html = await response.text();
        const nonce = html.match(/<script nonce="([^"]+)"/)?.[1];
        const scriptNonces = [...html.matchAll(/<script nonce="([^"]+)"/g)].map((match) => match[1]);

        expect(nonce).toBeDefined();
        expect(scriptNonces).toEqual([nonce, nonce]);
        expect(response.headers.get('content-security-policy')).toContain(`'nonce-${nonce}'`);
        expect(response.headers.get('content-security-policy')).toContain('ws:');
        expect(response.headers.get('cache-control')).toBe('no-store');
        expect(response.headers.has('content-length')).toBe(false);
        expect(response.headers.has('etag')).toBe(false);
    });

    it('leaves non-HTML responses untouched', async () => {
        const originalResponse = Response.json({ status: 'ok' });

        const response = await secureHtmlResponse(originalResponse, { allowDevelopmentConnections: true });

        expect(response).toBe(originalResponse);
        expect(response.headers.has('content-security-policy')).toBe(false);
        expect(response.headers.has('cache-control')).toBe(false);
    });

    it.each([204, 205, 304])('leaves bodyless HTML responses untouched for status %i', async (status) => {
        const originalResponse = new Response(null, {
            headers: { 'content-type': 'text/html; charset=utf-8' },
            status
        });

        const response = await secureHtmlResponse(originalResponse, { allowDevelopmentConnections: true });

        expect(response).toBe(originalResponse);
        expect(response.headers.has('content-security-policy')).toBe(false);
    });
});

function getDirective(policy: string, name: string): string[] {
    const directive = policy.split('; ').find((value) => value.startsWith(`${name} `));

    if (!directive) {
        throw new Error(`Missing ${name} directive.`);
    }

    return directive.slice(name.length + 1).split(' ');
}

function normalizeDevelopmentPolicy(policy: string): Record<string, string[]> {
    return Object.fromEntries(
        policy.split('; ').map((directive) => {
            const [name, ...sources] = directive.split(' ');
            const developmentSources = sources.filter((source) => source === 'ws:' || source === 'wss:');

            if (name === 'connect-src') {
                expect(developmentSources).toEqual(['ws:', 'wss:']);
            } else {
                expect(developmentSources).toEqual([]);
            }

            return [name, sources.filter((source) => !source.startsWith("'nonce-") && source !== 'ws:' && source !== 'wss:').sort()];
        })
    );
}

function readPolicyContract(): Record<string, string[]> {
    const contractPath = resolve(process.cwd(), '../Security/frontend-content-security-policy.contract.json');
    const contract = JSON.parse(readFileSync(contractPath, 'utf8')) as Record<string, string[]>;

    return Object.fromEntries(Object.entries(contract).map(([name, sources]) => [name, [...sources].sort()]));
}
