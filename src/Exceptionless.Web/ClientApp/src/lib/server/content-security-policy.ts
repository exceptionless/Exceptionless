import { randomBytes } from 'node:crypto';

const NONCE_BYTE_LENGTH = 32;
const NONCE_PATTERN = /^[A-Za-z\d+/]{43}=$/;
const NONCE_ATTRIBUTE_PATTERN = /\s+nonce(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+))?/gi;
const SCRIPT_OPENING_TAG_PATTERN = /<script\b((?:"[^"]*"|'[^']*'|[^'">])*)>/gi;

// Exceptionless uses Intercom's US endpoints. Keep region-specific sources scoped to that workspace.
const intercomChildSources = [
    'https://intercom-sheets.com',
    'https://www.intercom-reporting.com',
    'https://www.youtube.com',
    'https://player.vimeo.com',
    'https://fast.wistia.net'
] as const;

const intercomDownloadSources = ['https://downloads.intercomcdn.com'] as const;

const intercomUploadSources = ['https://uploads.intercomcdn.com', 'https://uploads.intercomusercontent.com'] as const;

const intercomAttachmentSources = [
    'https://*.intercom-attachments-1.com',
    'https://*.intercom-attachments-2.com',
    'https://*.intercom-attachments-3.com',
    'https://*.intercom-attachments-4.com',
    'https://*.intercom-attachments-5.com',
    'https://*.intercom-attachments-6.com',
    'https://*.intercom-attachments-7.com',
    'https://*.intercom-attachments-8.com',
    'https://*.intercom-attachments-9.com'
] as const;

const contentSecurityPolicyDirectives: ReadonlyArray<readonly [string, readonly string[]]> = [
    ['default-src', ["'self'"]],
    [
        'script-src',
        [
            "'strict-dynamic'",
            "'self'",
            'https://js.stripe.com',
            'https://*.js.stripe.com',
            'https://maps.googleapis.com',
            'https://app.intercom.io',
            'https://widget.intercom.io',
            'https://js.intercomcdn.com',
            'https://cdn.jsdelivr.net'
        ]
    ],
    ['script-src-attr', ["'none'"]],
    ['style-src', ["'self'", "'unsafe-inline'", 'https://fonts.googleapis.com', 'https://cdn.jsdelivr.net']],
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
            'https://gifs.intercomcdn.com',
            'https://video-messages.intercomcdn.com',
            'https://messenger-apps.intercom.io',
            ...intercomDownloadSources,
            ...intercomUploadSources,
            ...intercomAttachmentSources,
            'https://user-images.githubusercontent.com',
            'https://www.gravatar.com'
        ]
    ],
    ['font-src', ["'self'", 'https://fonts.gstatic.com', 'https://js.intercomcdn.com', 'https://fonts.intercomcdn.com', 'https://cdn.jsdelivr.net']],
    [
        'connect-src',
        [
            "'self'",
            'https://collector.exceptionless.io',
            'https://config.exceptionless.io',
            'https://heartbeat.exceptionless.io',
            'https://api.stripe.com',
            'https://maps.googleapis.com',
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
            'wss://nexus-websocket-b.intercom.io',
            ...intercomUploadSources
        ]
    ],
    [
        'frame-src',
        [
            "'self'",
            'https://js.stripe.com',
            'https://*.js.stripe.com',
            'https://hooks.stripe.com',
            'https://link.com',
            'https://*.link.com',
            ...intercomChildSources
        ]
    ],
    ['media-src', ["'self'", 'blob:', 'https://js.intercomcdn.com', ...intercomDownloadSources]],
    ['worker-src', ["'self'", 'blob:', ...intercomChildSources]],
    ['form-action', ["'self'", 'https://intercom.help', 'https://api-iam.intercom.io']],
    ['manifest-src', ["'self'"]],
    ['base-uri', ["'none'"]],
    ['object-src', ["'none'"]],
    ['frame-ancestors', ["'none'"]]
];

interface ContentSecurityPolicyOptions {
    allowDevelopmentConnections?: boolean;
}

export function addNonceToScripts(html: string, nonce: string): string {
    validateNonce(nonce);

    return html.replace(SCRIPT_OPENING_TAG_PATTERN, (openingTag, attributes: string) => {
        const attributesWithoutNonce = attributes.replace(NONCE_ATTRIBUTE_PATTERN, '');
        const scriptTagName = openingTag.slice(0, '<script'.length);

        return `${scriptTagName} nonce="${nonce}"${attributesWithoutNonce}>`;
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

            return `${directive} ${effectiveSources.join(' ')}`;
        })
        .join('; ');
}

export function createNonce(): string {
    return randomBytes(NONCE_BYTE_LENGTH).toString('base64');
}

export async function secureHtmlResponse(response: Response, options: ContentSecurityPolicyOptions = {}): Promise<Response> {
    if (!response.headers.get('content-type')?.startsWith('text/html')) {
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
