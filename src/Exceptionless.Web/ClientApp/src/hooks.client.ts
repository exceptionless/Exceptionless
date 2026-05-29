import type { ClientInit, HandleClientError } from '@sveltejs/kit';

import { dev } from '$app/environment';
import { page } from '$app/state';
import { env } from '$env/dynamic/public';
import { normalizePath, normalizeRouteId } from '$lib/telemetry';
import { Exceptionless, guid, toError } from '@exceptionless/browser';
import { useMiddleware } from '@exceptionless/fetchclient';

// If any of these are set in local storage, use them instead of the build-time environment variables.
// This allows you to target other environments from your browser without a rebuild.
const PUBLIC_BASE_URL = localStorage?.getItem('PUBLIC_BASE_URL');
if (PUBLIC_BASE_URL) {
    env.PUBLIC_BASE_URL = PUBLIC_BASE_URL;
}

const PUBLIC_EXCEPTIONLESS_API_KEY = localStorage?.getItem('PUBLIC_EXCEPTIONLESS_API_KEY');
if (PUBLIC_EXCEPTIONLESS_API_KEY) {
    env.PUBLIC_EXCEPTIONLESS_API_KEY = PUBLIC_EXCEPTIONLESS_API_KEY;
}

const PUBLIC_EXCEPTIONLESS_SERVER_URL = localStorage?.getItem('PUBLIC_EXCEPTIONLESS_SERVER_URL');
if (PUBLIC_EXCEPTIONLESS_SERVER_URL) {
    env.PUBLIC_EXCEPTIONLESS_SERVER_URL = PUBLIC_EXCEPTIONLESS_SERVER_URL;
}

export const init: ClientInit = async () => {
    if (!env.PUBLIC_EXCEPTIONLESS_API_KEY) {
        return;
    }

    await Exceptionless.startup((c) => {
        c.apiKey = env.PUBLIC_EXCEPTIONLESS_API_KEY;
        c.serverUrl = env.PUBLIC_EXCEPTIONLESS_SERVER_URL || window.location.origin;
        c.defaultTags.push('UI', 'Svelte');

        if (env.PUBLIC_APP_VERSION) {
            c.version = env.PUBLIC_APP_VERSION;
        }

        if (dev) {
            c.useDebugLogger();
            c.settings['@@log:*'] = 'debug';
        }

        c.useSessions();

        c.addPlugin('route-context', 10, async (ctx) => {
            if (ctx.event.type !== 'usage') {
                ctx.event.data = ctx.event.data ?? {};
                ctx.event.data['@route'] = normalizeRouteId(page.route.id);
            }
        });
    });

    useMiddleware(async (ctx, next) => {
        await next();

        const status = ctx.response?.status;
        if (!status || (status < 500 && status !== 429)) {
            return;
        }

        const rawUrl = ctx.request?.url ?? '';
        if (rawUrl.includes('/api/v2/events') || rawUrl.includes('/api/v2/configuration')) {
            return;
        }

        const method = ctx.request?.method ?? 'UNKNOWN';
        let pathname = rawUrl;
        try {
            pathname = new URL(rawUrl).pathname;
        } catch {
            /* relative or malformed URL — use as-is */
        }

        const path = normalizePath(pathname, '');
        void Exceptionless.createLog(`${method} ${path}`, `HTTP ${status}`, 'warn').addTags('api-failure').submit();
    });
};

export const handleError: HandleClientError = async ({ error, event, message, status }) => {
    if (dev) {
        console.warn({ error, event, message, status });
    }

    let errorId: null | string = null;
    try {
        await Exceptionless.createException(toError(error ?? message))
            .setProperty('status', String(status))
            .submit();
        errorId = Exceptionless.getLastReferenceId();
    } catch {
        // never throw
    }

    return { errorId: errorId ?? guid(), message };
};
