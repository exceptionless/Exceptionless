import type { ClientInit, HandleClientError } from '@sveltejs/kit/hooks';

import { dev } from '$app/env';
import { PUBLIC_APP_VERSION, PUBLIC_ENABLE_SSL, PUBLIC_EXCEPTIONLESS_TELEMETRY_SERVER_URL } from '$app/env/public';
import { page } from '$app/state';
import { configureSessions } from '$features/auth/exceptionless-session';
import { getHttpsRedirectUrl } from '$features/navigation/https-redirect';
import { canonicalAppUrl } from '$features/navigation/legacy-links';
import { getEnvironmentOverride, PUBLIC_EXCEPTIONLESS_API_KEY, PUBLIC_EXCEPTIONLESS_SERVER_URL } from '$features/shared/environment';
import { trackHistoryTraversals } from '$features/shared/history-traversal';
import { normalizePath, normalizeRouteId } from '$lib/telemetry';
import { installSvelteEffectDepthDiagnostics } from '$lib/telemetry/svelte-effect-depth-diagnostics';
import { Exceptionless, guid, toError } from '@exceptionless/browser';
import { useMiddleware } from '@foundatiofx/fetchclient';

installSvelteEffectDepthDiagnostics();

function normalizeCurrentAppUrl() {
    const canonical = canonicalAppUrl(new URL(window.location.href));
    if (canonical.href !== window.location.href) {
        window.history.replaceState(window.history.state, '', canonical);
    }
}

export const init: ClientInit = async () => {
    const httpsDestination = getHttpsRedirectUrl(new URL(window.location.href), PUBLIC_ENABLE_SSL);
    if (httpsDestination) {
        window.location.replace(httpsDestination.href);
        // Keep the router, authentication, and telemetry from starting on the HTTP page.
        await new Promise<void>(() => {});
        return;
    }

    normalizeCurrentAppUrl();
    // Normalize existing entries before the router reads them; cancelling popstate would undo Back/Forward.
    window.addEventListener('popstate', normalizeCurrentAppUrl, { capture: true });
    trackHistoryTraversals();

    if (!PUBLIC_EXCEPTIONLESS_API_KEY) {
        return;
    }

    await Exceptionless.startup((c) => {
        c.apiKey = PUBLIC_EXCEPTIONLESS_API_KEY;
        c.serverUrl =
            getEnvironmentOverride('PUBLIC_EXCEPTIONLESS_SERVER_URL') ||
            (PUBLIC_EXCEPTIONLESS_TELEMETRY_SERVER_URL ?? PUBLIC_EXCEPTIONLESS_SERVER_URL) ||
            window.location.origin;
        c.defaultTags.push('UI', 'Svelte');

        if (PUBLIC_APP_VERSION) {
            c.version = PUBLIC_APP_VERSION;
        }

        if (dev) {
            c.useDebugLogger();
            c.settings['@@log:*'] = 'debug';
        }

        configureSessions(c);

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
        if (!status || (status !== 404 && status < 500 && status !== 429)) {
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

        try {
            if (status === 404) {
                await Exceptionless.submitNotFound(`${method} ${path}`);
            } else {
                await Exceptionless.createLog(`${method} ${path}`, `HTTP ${status}`, 'warn').addTags('api-failure').submit();
            }
        } catch {
            // never let telemetry break API calls
        }
    });
};

export const handleError: HandleClientError = async ({ error, event, kind }) => {
    if (dev) {
        console.warn({ error, event, kind });
    }

    // Errors created with error(...) are expected and already safe to show.
    if (kind === 'app') {
        return error;
    }

    let errorId: null | string = null;
    try {
        if (kind === 'framework' && error.status === 404) {
            await Exceptionless.submitNotFound(normalizePath(event.url.pathname));
        } else {
            await Exceptionless.createException(toError(kind === 'framework' ? error.message : error))
                .setProperty('status', String(kind === 'framework' ? error.status : 500))
                .submit();
        }

        errorId = Exceptionless.getLastReferenceId();
    } catch {
        // never throw
    }

    return { errorId: errorId ?? guid() };
};
