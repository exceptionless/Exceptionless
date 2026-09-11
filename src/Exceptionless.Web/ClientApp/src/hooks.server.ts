import type { Handle } from '@sveltejs/kit';

import { building } from '$app/environment';
import { secureHtmlResponse } from '$lib/server/content-security-policy';

export const handle: Handle = async ({ event, resolve }) => {
    const response = await resolve(event);

    if (building) {
        return response;
    }

    return secureHtmlResponse(response, { allowDevelopmentConnections: true });
};
