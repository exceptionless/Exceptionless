import { PUBLIC_EXCEPTIONLESS_API_KEY, PUBLIC_EXCEPTIONLESS_SERVER_URL } from '$env/static/public';

import { Exceptionless, toError } from '@exceptionless/browser';

await Exceptionless.startup((c) => {
	c.apiKey = PUBLIC_EXCEPTIONLESS_API_KEY;
	c.serverUrl = PUBLIC_EXCEPTIONLESS_SERVER_URL || window.location.origin;

	c.defaultTags.push('UI', 'Svelte');
	c.settings['@@log:*'] = 'debug';
});

/** @type {import('@sveltejs/kit').HandleClientError} */
export async function handleError({ error, event }) {
	console.log('client error handler', event);
	await Exceptionless.submitException(toError(error));
}
