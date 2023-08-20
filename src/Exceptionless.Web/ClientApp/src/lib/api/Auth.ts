import { derived } from 'svelte/store';
import { persisted } from 'svelte-local-storage-store';
import { FetchClient } from './FetchClient';
import type { TokenResult } from '$lib/models/api.generated';
import { goto } from '$app/navigation';

export const accessToken = persisted<string | null>('access_token', null);
export const isAuthenticated = derived(accessToken, ($accessToken) => $accessToken !== null);

export async function logout() {
	const client = new FetchClient();
	await client.get('auth/logout', { expectedStatusCodes: [200, 401] });
	accessToken.set(null);
}

// TODO move client ids out to config
export async function liveLogin(redirectUrl?: string) {
	await oauthLogin({
		provider: 'live',
		clientId: '000000004C137E8B',
		authUrl: 'https://login.live.com/oauth20_authorize.srf',
		scope: 'wl.emails',
		extraParams: {
			display: 'popup'
		},
		redirectUrl
	});
}

export async function facebookLogin(redirectUrl?: string) {
	await oauthLogin({
		provider: 'facebook',
		clientId: '395178683904310',
		authUrl: 'https://www.facebook.com/v2.5/dialog/oauth',
		scope: 'email',
		redirectUrl
	});
}

export async function googleLogin(redirectUrl?: string) {
	await oauthLogin({
		provider: 'google',
		clientId: '809763155066-enkkdmt4ierc33q9cft9nf5d5c02h30q.apps.googleusercontent.com',
		authUrl: 'https://accounts.google.com/o/oauth2/auth/oauthchooseaccount',
		scope: 'openid profile email',
		extraParams: {
			state: encodeURIComponent(Math.random().toString(36).substring(2)),
			display: 'popup',
			service: 'lso',
			o2v: '1',
			flowName: 'GeneralOAuthFlow'
		},
		redirectUrl
	});
}

export async function githubLogin(redirectUrl?: string) {
	await oauthLogin({
		provider: 'github',
		clientId: '7ef1dd5bfbc4ccf7f5ef',
		authUrl: 'https://github.com/login/oauth/authorize',
		scope: 'user:email',
		popupOptions: { width: 1020, height: 618 },
		redirectUrl
	});
}

async function oauthLogin(options: {
	provider: string;
	clientId: string;
	authUrl: string;
	scope: string;
	popupOptions?: { width: number; height: number };
	extraParams?: Record<string, string>;
	redirectUrl?: string;
}) {
	const width = options.popupOptions?.width || 500;
	const height = options.popupOptions?.height || 500;
	const features = {
		width: width,
		height: height,
		top: window.screenY + (window.outerHeight - height) / 2.5,
		left: window.screenX + (window.outerWidth - width) / 2
	};

	const redirectUrl = window.location.origin;
	const params = Object.assign(
		{
			response_type: 'code',
			client_id: options.clientId,
			redirect_uri: redirectUrl,
			scope: options.scope
		},
		options.extraParams
	);

	const url = `${options.authUrl}?${new URLSearchParams(params).toString()}`;

	const popup = window.open(url, options.provider, stringifyOptions(features));
	popup?.focus();

	const data = await waitForUrl(popup!, redirectUrl);
	if (options.extraParams?.state && data.state !== options.extraParams.state)
		throw new Error('Invalid state');

	const client = new FetchClient();
	const response = await client.postJSON<TokenResult>(`auth/${options.provider}`, {
		state: data.state,
		code: data.code,
		clientId: options.clientId,
		redirectUri: redirectUrl
	});

	if (response.success && response.data?.token) {
		accessToken.set(response.data.token);
		await goto(options.redirectUrl || '/');
	}
}

function waitForUrl(popup: Window, redirectUri: string): Promise<{ state: string; code: string }> {
	return new Promise((resolve, reject) => {
		const polling = setInterval(() => {
			if (!popup || popup.closed || popup.closed === undefined) {
				clearInterval(polling);
				reject(new Error('The popup window was closed'));
			}

			try {
				if (popup?.location.href.startsWith(redirectUri)) {
					if (popup.location.search || popup.location.hash) {
						const query = Object.fromEntries(
							new URLSearchParams(
								popup.location.search.substring(1).replace(/\/$/, '')
							)
						);
						const hash = Object.fromEntries(
							new URLSearchParams(
								popup.location.hash.substring(1).replace(/[/$]/, '')
							)
						);
						const params = Object.assign({}, query, hash) as {
							state: string;
							code: string;
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
				console.log(error);
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
