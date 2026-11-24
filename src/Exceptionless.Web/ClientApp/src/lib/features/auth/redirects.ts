import { resolve } from '$app/paths';

import { getSafeRedirectUrl } from '#features/shared/url.ts';

/** Query parameters the login and signup pages pass along to each other and through OAuth. */
export interface AuthPageParams {
    /** A same-origin path to open after authentication. Unsafe values are dropped. */
    redirect: null | string;
    /** An organization invitation token. */
    token: null | string;
}

/** Builds a login or signup link that keeps the redirect and invitation. */
export function buildAuthPageHref(path: string, { redirect, token }: AuthPageParams): string {
    const search = new URLSearchParams();
    if (redirect) {
        search.set('redirect', redirect);
    }

    if (token) {
        search.set('token', token);
    }

    const query = search.toString();
    return query ? `${path}?${query}` : path;
}

export function getAuthPageParams(searchParams: Pick<URLSearchParams, 'get'>): AuthPageParams {
    return {
        redirect: getSafeRedirectUrl(searchParams.get('redirect'), null),
        token: searchParams.get('token')?.trim() || null
    };
}

export function getPostLoginRedirect({ redirect }: AuthPageParams): string {
    return redirect ?? resolve('/(app)');
}

/**
 * Where a new account lands. Invited users join an existing organization, so they go to the
 * app; everyone else creates their first organization. OAuth can sign in an existing account
 * from the signup page, so it lands in the app, which sends first-run users to setup.
 */
export function getPostSignupRedirect({ redirect, token }: AuthPageParams, method: 'oauth' | 'password'): string {
    if (redirect) {
        return redirect;
    }

    if (token || method === 'oauth') {
        return resolve('/(app)');
    }

    return resolve('/(app)/organization/add');
}
