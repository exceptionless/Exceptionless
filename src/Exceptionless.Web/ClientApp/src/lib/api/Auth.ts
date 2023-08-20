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

// TODO refactor this to be used with multiple providers
export async function googleLogin() {
	const width = 500;
	const height = 500;
	const options = {
		width: width,
		height: height,
		top: window.screenY + (window.outerHeight - height) / 2.5,
		left: window.screenX + (window.outerWidth - width) / 2
	};

	// TODO move this to config
	const clientId = '809763155066-enkkdmt4ierc33q9cft9nf5d5c02h30q.apps.googleusercontent.com';
	const redirectUrl = 'http://localhost:5173';
	const state = encodeURIComponent(Math.random().toString(36).substring(2));
	const params = new URLSearchParams({
		response_type: 'code',
		client_id: clientId,
		redirect_uri: redirectUrl,
		scope: 'openid profile email',
		display: 'popup',
		state: state,
		service: 'lso',
		o2v: '1',
		flowName: 'GeneralOAuthFlow'
	});
	const url = `https://accounts.google.com/o/oauth2/auth/oauthchooseaccount?${params.toString()}`;

	const popup = window.open(url, 'google', stringifyOptions(options));
	popup?.focus();

	const data = await waitForUrl(popup!, redirectUrl);
	if (data.state !== state) throw new Error('Invalid state');

	const client = new FetchClient();
	const response = await client.postJSON<TokenResult>('auth/google', {
		state: data.state,
		code: data.code,
		clientId: clientId,
		redirectUri: redirectUrl
	});

	if (response.success && response.data?.token) {
		accessToken.set(response.data.token);
		await goto('/');
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
