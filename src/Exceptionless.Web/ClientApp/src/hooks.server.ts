import type { Handle } from '@sveltejs/kit';

import { building, dev } from '$app/environment';
import { env } from '$env/dynamic/public';
import { secureHtmlResponse } from '$lib/server/content-security-policy';

export const handle: Handle = async ({ event, resolve }) => {
    const response = await resolve(event);

    if (building) {
        return response;
    }

    return secureHtmlResponse(response, { allowDevelopmentConnections: dev, siteBaseUrl: env.PUBLIC_BASE_URL });
};
