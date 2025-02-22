import type { ServerInit } from '@sveltejs/kit';

import { env } from '$env/dynamic/public';
import { Exceptionless, toError } from '@exceptionless/browser';

// If the PUBLIC_BASE_URL is set in local storage, we will use that instead of the one from the environment variables.
// This allows you to target other environments from your browser.
const PUBLIC_BASE_URL = localStorage?.getItem('PUBLIC_BASE_URL');
if (PUBLIC_BASE_URL) {
    env.PUBLIC_BASE_URL = PUBLIC_BASE_URL;
}

export const init: ServerInit = async () => {
    await Exceptionless.startup((c) => {
        c.apiKey = env.PUBLIC_EXCEPTIONLESS_API_KEY;
        c.serverUrl = env.PUBLIC_EXCEPTIONLESS_SERVER_URL || window.location.origin;

        c.defaultTags.push('UI', 'Svelte');
        c.settings['@@log:*'] = 'debug';
    });
};

/** @type {import('@sveltejs/kit').HandleClientError} */
export async function handleError({ error, event, message, status }) {
    console.warn({ error, event, message, source: 'client error handler', status });
    await Exceptionless.createException(toError(error ?? message))
        .setProperty('status', status)
        .submit();
}
