import type { Handle } from '@sveltejs/kit';

import { building, dev } from '$app/environment';
import { env } from '$env/dynamic/public';
import { secureHtmlResponse } from '$lib/server/content-security-policy';

export const handle: Handle = async ({ event, resolve }) => {
    const path = event.url.pathname;
    const isDocumentRequest = ['GET', 'HEAD'].includes(event.request.method) && (!/\/[^/]*\.[^/]+$/.test(path) || path.toLowerCase().endsWith('.html'));
    if (!building && isDocumentRequest) {
        for (const header of ['if-none-match', 'if-modified-since', 'range', 'if-range']) {
            event.request.headers.delete(header);
        }
    }

    const response = await resolve(event);

    if (building) {
        return response;
    }

    // The checked-in .env uses an empty value for the same-origin default.
    return secureHtmlResponse(response, { allowDevelopmentConnections: dev, siteBaseUrl: env.PUBLIC_BASE_URL || undefined });
};
