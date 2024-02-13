import { env } from '$env/dynamic/public';

import { Exceptionless, toError } from '@exceptionless/browser';

await Exceptionless.startup((c) => {
    c.apiKey = env.PUBLIC_EXCEPTIONLESS_API_KEY;
    c.serverUrl = env.PUBLIC_EXCEPTIONLESS_SERVER_URL || window.location.origin;

    c.defaultTags.push('UI', 'Svelte');
    c.settings['@@log:*'] = 'debug';
});

/** @type {import('@sveltejs/kit').HandleClientError} */
export async function handleError({ error, event }) {
    console.log('client error handler', event);
    await Exceptionless.submitException(toError(error));
}
