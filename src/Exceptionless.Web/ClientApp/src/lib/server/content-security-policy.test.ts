import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

import { createContentSecurityPolicy, createNonce, getWebSocketOrigin, replaceScriptNonce, secureHtmlResponse } from './content-security-policy';

describe('configured WebSocket origin', () => {
    it.each([
        ['https://app.example.test/next/?query=value#fragment', 'wss://app.example.test'],
        ['https://app.example.test:8443/next', 'wss://app.example.test:8443'],
        ['http://localhost:7110', 'ws://localhost:7110'],
        ['http://localhost:80', 'ws://localhost'],
        ['https://[::1]:8443/next', 'wss://[::1]:8443']
    ])('allows only the configured origin for %s', (siteBaseUrl, expectedOrigin) => {
        const policy = createContentSecurityPolicy(createNonce(), { siteBaseUrl });

        expect(getWebSocketOrigin(siteBaseUrl)).toBe(expectedOrigin);
        expect(getDirective(policy, 'connect-src')).toContain(expectedOrigin);
        expect(getDirective(policy, 'connect-src')).not.toContain('ws:');
        expect(getDirective(policy, 'connect-src')).not.toContain('wss:');
        expect(policy).not.toContain('query=value');
    });

    it.each(['', '/next', 'ftp://app.example.test', 'https://user:password@app.example.test', 'https://*.example.test'])(
        'rejects invalid configuration %s',
        (siteBaseUrl) => {
            expect(() => createContentSecurityPolicy(createNonce(), { siteBaseUrl })).toThrow();
        }
    );
});

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

describe('replaceScriptNonce', () => {
    it('replaces only SvelteKit-authorized script nonces, preserving untrusted tags and quoted attribute text', () => {
        const trustedNonce = createNonce();
        const nonce = createNonce();
        const html =
            `<script data-note="a nonce='${trustedNonce}'" nonce="${trustedNonce}">const marker = "<script>";</script>` +
            `<script nonce='${trustedNonce}' src="/app.js"></script>` +
            `<script nonce="attacker">injected()</script><script>injected()</script><script src="https://untrusted.example/payload.js"></script>`;

        const result = replaceScriptNonce(html, trustedNonce, nonce);

        expect(result).toContain(`<script data-note="a nonce='${trustedNonce}'" nonce="${nonce}">const marker = "<script>";</script>`);
        expect(result).toContain(`<script nonce="${nonce}" src="/app.js"></script>`);
        expect(result).toContain(
            '<script nonce="attacker">injected()</script><script>injected()</script><script src="https://untrusted.example/payload.js"></script>'
        );
        expect([...result.matchAll(new RegExp(`nonce="${nonce.replaceAll('+', '\\+')}"`, 'g'))]).toHaveLength(2);
    });
});

describe('createContentSecurityPolicy', () => {
    it('excludes unused vendor capabilities while preserving core payment and messenger dependencies', () => {
        const policy = createContentSecurityPolicy(createNonce());

        expect(policy).not.toContain('fonts.googleapis.com');
        expect(policy).not.toContain('fonts.gstatic.com');
        expect(policy).not.toContain('user-images.githubusercontent.com');
        for (const unusedSource of [
            'config.exceptionless.io',
            'heartbeat.exceptionless.io',
            'maps.googleapis.com',
            'cdn.jsdelivr.net',
            'intercom-sheets.com',
            'intercom-reporting.com',
            'youtube.com',
            'vimeo.com',
            'wistia.net',
            'intercom-attachments-',
            'uploads.intercom',
            'downloads.intercom',
            'gifs.intercom',
            'video-messages.intercom',
            'messenger-apps.intercom',
            'intercom.help'
        ]) {
            expect(policy).not.toContain(unusedSource);
        }
        expect(getDirective(policy, 'img-src')).not.toContain('https://*.stripe.com');
        expect(getDirective(policy, 'connect-src')).toContain('https://api.stripe.com');
        expect(getDirective(policy, 'form-action')).toEqual(["'self'"]);
        expect(getDirective(policy, 'worker-src')).toEqual(["'self'"]);
        expect(getDirective(policy, 'connect-src')).toContain('https://*.intercom-messenger.com');
        expect(getDirective(policy, 'connect-src')).toContain('wss://*.intercom-messenger.com');
    });

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
    it('buffers chunked HTML, preserves framework trust, and removes stale response metadata', async () => {
        const trustedNonce = createNonce();
        const encoder = new TextEncoder();
        const stream = new ReadableStream<Uint8Array>({
            start(controller) {
                controller.enqueue(encoder.encode('<!doctype html><html><body><scr'));
                controller.enqueue(
                    encoder.encode(
                        `ipt nonce="${trustedNonce}" type="module">start()</script><script nonce="${trustedNonce}" src="/app.js"></script><script>injected()</script></body></html>`
                    )
                );
                controller.close();
            }
        });
        const originalResponse = new Response(stream, {
            headers: {
                'accept-ranges': 'bytes',
                'content-length': '123',
                'content-security-policy': `script-src 'nonce-${trustedNonce}' 'strict-dynamic'`,
                'content-type': 'text/html; charset=utf-8',
                etag: 'stale-after-transformation',
                'last-modified': 'Wed, 30 Sep 2026 00:00:00 GMT'
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
        expect(response.headers.has('last-modified')).toBe(false);
        expect(response.headers.has('accept-ranges')).toBe(false);
        expect(html).toContain('<script>injected()</script>');
    });

    it('does not grant a nonce when the framework has not authorized any scripts', async () => {
        const html = '<script>injected()</script><script nonce="attacker" src="/payload.js"></script>';
        const response = await secureHtmlResponse(new Response(html, { headers: { 'content-type': 'text/html' } }));

        expect(await response.text()).toBe(html);
        expect(response.headers.get('content-security-policy')).toContain("'strict-dynamic'");
        expect(response.headers.get('cache-control')).toBe('no-store');
    });

    it.each([200, 206])('rejects partial HTML rather than rewriting its byte range (status %i)', async (status) => {
        const originalResponse = new Response('<script>start()</script>', {
            headers: { 'content-range': 'bytes 0-23/100', 'content-type': 'text/html' },
            status
        });

        await expect(secureHtmlResponse(originalResponse)).rejects.toThrow('complete HTML document');
    });

    it('secures HEAD metadata without manufacturing a response body', async () => {
        const response = await secureHtmlResponse(
            new Response(null, {
                headers: { 'accept-ranges': 'bytes', 'content-type': 'text/html', etag: 'stale', 'last-modified': 'Wed, 30 Sep 2026 00:00:00 GMT' }
            })
        );

        expect(response.body).toBeNull();
        expect(response.headers.get('cache-control')).toBe('no-store');
        expect(response.headers.has('etag')).toBe(false);
        expect(response.headers.has('last-modified')).toBe(false);
        expect(response.headers.has('accept-ranges')).toBe(false);
        expect(response.headers.get('content-security-policy')).toContain("'strict-dynamic'");
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
