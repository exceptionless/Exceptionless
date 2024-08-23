import { goto } from '$app/navigation';
import { page } from '$app/stores';
import { env } from '$env/dynamic/public';
import { AuthJSONSerializer, persisted } from '$shared/persisted.svelte';
import { useFetchClient } from '@exceptionless/fetchclient';
import { get } from 'svelte/store';

import type { Login, TokenResult } from './models';

export const accessToken = persisted('satellizer_token', null, new AuthJSONSerializer());

export const enableAccountCreation = env.PUBLIC_ENABLE_ACCOUNT_CREATION === 'true';
export const facebookClientId = env.PUBLIC_FACEBOOK_APPID;
export const gitHubClientId = env.PUBLIC_GITHUB_APPID;
export const googleClientId = env.PUBLIC_GOOGLE_APPID;
export const microsoftClientId = env.PUBLIC_MICROSOFT_APPID;
export const enableOAuthLogin = facebookClientId || gitHubClientId || googleClientId || microsoftClientId;

export async function login(email: string, password: string) {
    const data: Login = { email, password };
    const client = useFetchClient();
    const response = await client.postJSON<TokenResult>('auth/login', data, {
        expectedStatusCodes: [401]
    });

    if (response.ok && response.data?.token) {
        accessToken.value = response.data.token;
    } else if (response.status === 401) {
        response.problem.setErrorMessage('Invalid email or password');
    }

    return response;
}

export async function gotoLogin() {
    const { url } = get(page);
    const isAuthPath = url.pathname.startsWith('/next/login') || url.pathname.startsWith('/next/logout');
    const redirect = url.pathname === '/next/' || isAuthPath ? '/next/login' : `/next/login?redirect=${url.pathname}`;
    await goto(redirect, { replaceState: true });
}

export async function logout() {
    const client = useFetchClient();
    await client.get('auth/logout', { expectedStatusCodes: [200, 401] });
    accessToken.value = null;
}

export async function liveLogin(redirectUrl?: string) {
    if (!microsoftClientId) {
        throw new Error('Live client id not set');
    }

    await oauthLogin({
        authUrl: 'https://login.live.com/oauth20_authorize.srf',
        clientId: microsoftClientId,
        extraParams: {
            display: 'popup'
        },
        provider: 'live',
        redirectUrl,
        scope: 'wl.emails'
    });
}

export async function facebookLogin(redirectUrl?: string) {
    if (!facebookClientId) {
        throw new Error('Facebook client id not set');
    }

    await oauthLogin({
        authUrl: 'https://www.facebook.com/v2.5/dialog/oauth',
        clientId: facebookClientId,
        provider: 'facebook',
        redirectUrl,
        scope: 'email'
    });
}

export async function googleLogin(redirectUrl?: string) {
    if (!googleClientId) {
        throw new Error('Google client id not set');
    }

    await oauthLogin({
        authUrl: 'https://accounts.google.com/o/oauth2/auth/oauthchooseaccount',
        clientId: googleClientId,
        extraParams: {
            display: 'popup',
            flowName: 'GeneralOAuthFlow',
            o2v: '1',
            service: 'lso',
            state: encodeURIComponent(Math.random().toString(36).substring(2))
        },
        provider: 'google',
        redirectUrl,
        scope: 'openid profile email'
    });
}

export async function githubLogin(redirectUrl?: string) {
    if (!gitHubClientId) {
        throw new Error('GitHub client id not set');
    }

    await oauthLogin({
        authUrl: 'https://github.com/login/oauth/authorize',
        clientId: gitHubClientId,
        popupOptions: { height: 618, width: 1020 },
        provider: 'github',
        redirectUrl,
        scope: 'user:email'
    });
}

async function oauthLogin(options: {
    authUrl: string;
    clientId: string;
    extraParams?: Record<string, string>;
    popupOptions?: { height: number; width: number };
    provider: string;
    redirectUrl?: string;
    scope: string;
}) {
    const width = options.popupOptions?.width || 500;
    const height = options.popupOptions?.height || 500;
    const features = {
        height: height,
        left: window.screenX + (window.outerWidth - width) / 2,
        top: window.screenY + (window.outerHeight - height) / 2.5,
        width: width
    };

    const redirectUrl = window.location.origin;
    const params = Object.assign(
        {
            client_id: options.clientId,
            redirect_uri: redirectUrl,
            response_type: 'code',
            scope: options.scope
        },
        options.extraParams
    );

    const url = `${options.authUrl}?${new URLSearchParams(params).toString()}`;

    const popup = window.open(url, options.provider, stringifyOptions(features));
    popup?.focus();

    const data = await waitForUrl(popup!, redirectUrl);
    if (options.extraParams?.state && data.state !== options.extraParams.state) throw new Error('Invalid state');

    const client = useFetchClient();
    const response = await client.postJSON<TokenResult>(`auth/${options.provider}`, {
        clientId: options.clientId,
        code: data.code,
        redirectUri: redirectUrl,
        state: data.state
    });

    if (response.ok && response.data?.token) {
        accessToken.value = response.data.token;
        await goto(options.redirectUrl || '/');
    }
}

function waitForUrl(popup: Window, redirectUri: string): Promise<{ code: string; state: string }> {
    return new Promise((resolve, reject) => {
        const polling = setInterval(() => {
            if (!popup || popup.closed || popup.closed === undefined) {
                clearInterval(polling);
                reject(new Error('The popup window was closed'));
            }

            try {
                if (popup?.location.href.startsWith(redirectUri)) {
                    if (popup.location.search || popup.location.hash) {
                        const query = Object.fromEntries(new URLSearchParams(popup.location.search.substring(1).replace(/\/$/, '')));
                        const hash = Object.fromEntries(new URLSearchParams(popup.location.hash.substring(1).replace(/[/$]/, '')));
                        const params = Object.assign({}, query, hash) as {
                            code: string;
                            state: string;
                        };

                        if ('error' in params && (params as { error: string }).error) {
                            reject(new Error((params as { error: string }).error));
                        } else {
                            resolve(params);
                        }
                    } else {
                        reject(
                            new Error(
                                'OAuth redirect has occurred but no query or hash parameters were found. ' +
                                    'They were either not set during the redirect, or were removed—typically by a ' +
                                    'routing library before they could be read.'
                            )
                        );
                    }

                    clearInterval(polling);
                    popup?.close();
                }
            } catch (error) {
                console.error(error);
                // Ignore DOMException: Blocked a frame with origin from accessing a cross-origin frame.
                // A hack to get around same-origin security policy errors in IE.
            }
        }, 500);
    });
}

function stringifyOptions(options: object): string {
    const parts = [];

    for (const [key, value] of Object.entries(options)) {
        parts.push(key + '=' + value);
    }

    return parts.join(',');
}
