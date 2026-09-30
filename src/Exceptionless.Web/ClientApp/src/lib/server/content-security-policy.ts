import { randomBytes } from 'node:crypto';

const NONCE_BYTE_LENGTH = 32;
const NONCE_PATTERN = /^[A-Za-z\d+/]{43}=$/;
const NONCE_ATTRIBUTE_PATTERN = /("[^"]*"|'[^']*')|\s+nonce(?=[\s=>/]|$)(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+))?/gi;
const SCRIPT_ELEMENT_PATTERN = /(<script\b)((?:"[^"]*"|'[^']*'|[^'">])*)>([\s\S]*?)(<\/script\s*>)/gi;

// Exceptionless uses Intercom's US endpoints. Keep region-specific sources scoped to that workspace.
const contentSecurityPolicyDirectives: ReadonlyArray<readonly [string, readonly string[]]> = [
    ['default-src', ["'self'"]],
    [
        'script-src',
        [
            "'strict-dynamic'",
            "'self'",
            'https://js.stripe.com',
            'https://*.js.stripe.com',
            'https://app.intercom.io',
            'https://widget.intercom.io',
            'https://js.intercomcdn.com'
        ]
    ],
    ['style-src', ["'self'", "'unsafe-inline'"]],
    [
        'img-src',
        [
            "'self'",
            'blob:',
            'data:',
            'https://*.stripe.com',
            'https://*.link.com',
            'https://js.intercomcdn.com',
            'https://static.intercomassets.com',
            'https://www.gravatar.com'
        ]
    ],
    ['font-src', ["'self'", 'https://js.intercomcdn.com', 'https://fonts.intercomcdn.com']],
    [
        'connect-src',
        [
            "'self'",
            'https://collector.exceptionless.io',
            'https://config.exceptionless.io',
            'https://heartbeat.exceptionless.io',
            'https://api.stripe.com',
            'https://link.com',
            'https://*.link.com',
            'https://via.intercom.io',
            'https://api.intercom.io',
            'https://api-iam.intercom.io',
            'https://api-ping.intercom.io',
            'https://*.intercom-messenger.com',
            'wss://*.intercom-messenger.com',
            'https://nexus-websocket-a.intercom.io',
            'wss://nexus-websocket-a.intercom.io',
            'https://nexus-websocket-b.intercom.io',
            'wss://nexus-websocket-b.intercom.io'
        ]
    ],
    ['frame-src', ["'self'", 'https://js.stripe.com', 'https://*.js.stripe.com', 'https://hooks.stripe.com', 'https://link.com', 'https://*.link.com']],
    ['media-src', ["'self'", 'blob:', 'https://js.intercomcdn.com']],
    ['worker-src', ["'self'", 'blob:']],
    ['form-action', ["'self'"]],
    ['manifest-src', ["'self'"]],
    ['base-uri', ["'none'"]],
    ['object-src', ["'none'"]],
    ['frame-ancestors', ["'none'"]]
];

interface ContentSecurityPolicyOptions {
    allowDevelopmentConnections?: boolean;
    siteBaseUrl?: string;
}

export function addNonceToScripts(html: string, nonce: string): string {
    validateNonce(nonce);

    return html.replace(SCRIPT_ELEMENT_PATTERN, (_scriptElement, scriptTagName: string, attributes: string, content: string, closingTag: string) => {
        const attributesWithoutNonce = attributes.replace(NONCE_ATTRIBUTE_PATTERN, (_attribute, quoted: string | undefined) => quoted ?? '');

        return `${scriptTagName} nonce="${nonce}"${attributesWithoutNonce}>${content}${closingTag}`;
    });
}

export function createContentSecurityPolicy(nonce: string, options: ContentSecurityPolicyOptions = {}): string {
    validateNonce(nonce);

    return contentSecurityPolicyDirectives
        .map(([directive, sources]) => {
            let effectiveSources = sources;
            if (directive === 'script-src') {
                effectiveSources = [`'nonce-${nonce}'`, ...sources];
            } else if (directive === 'connect-src' && options.allowDevelopmentConnections) {
                effectiveSources = [...sources, 'ws:', 'wss:'];
            }

            if (directive === 'connect-src' && options.siteBaseUrl !== undefined) {
                effectiveSources = [...effectiveSources, getWebSocketOrigin(options.siteBaseUrl)];
            }

            return `${directive} ${effectiveSources.join(' ')}`;
        })
        .join('; ');
}

export function createNonce(): string {
    return randomBytes(NONCE_BYTE_LENGTH).toString('base64');
}

export function getWebSocketOrigin(siteBaseUrl: string): string {
    const url = new URL(siteBaseUrl);
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.hostname.includes('*')) {
        throw new Error('The CSP site base URL must be an absolute HTTP(S) URL without credentials or wildcard hosts.');
    }

    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    return url.origin;
}

export async function secureHtmlResponse(response: Response, options: ContentSecurityPolicyOptions = {}): Promise<Response> {
    if (!response.headers.get('content-type')?.startsWith('text/html') || response.body === null) {
        return response;
    }

    const nonce = createNonce();
    const html = addNonceToScripts(await response.text(), nonce);
    const headers = new Headers(response.headers);
    headers.delete('content-encoding');
    headers.delete('content-length');
    headers.delete('etag');
    headers.set('Cache-Control', 'no-store');
    headers.set('Content-Security-Policy', createContentSecurityPolicy(nonce, options));

    return new Response(html, {
        headers,
        status: response.status,
        statusText: response.statusText
    });
}

function validateNonce(nonce: string): void {
    if (!NONCE_PATTERN.test(nonce)) {
        throw new Error('CSP nonce must be a base64-encoded 32-byte value.');
    }
}
