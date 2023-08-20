import { derived } from 'svelte/store';
import { persisted } from 'svelte-local-storage-store';

export const accessToken = persisted<string | null>('access_token', null);
export const isAuthenticated = derived(accessToken, ($accessToken) => $accessToken !== null);

export function logout() {
	accessToken.set(null);
}

export async function googleLogin() {
	const width = 500;
	const height = 500;
	const options = {
		width: width,
		height: height,
		top: window.screenY + (window.outerHeight - height) / 2.5,
		left: window.screenX + (window.outerWidth - width) / 2
	};
	const clientId = '809763155066-enkkdmt4ierc33q9cft9nf5d5c02h30q.apps.googleusercontent.com';
	const redirectUrl = 'https://be.exceptionless.io';
	const url = `https://accounts.google.com/o/oauth2/auth/oauthchooseaccount?response_type=code&client_id=${clientId}&redirect_uri=${redirectUrl}&scope=openid%20profile%20email&display=popup&state=vl1jzjidwe7&service=lso&o2v=1&flowName=GeneralOAuthFlow`;

	const popup = window.open(url, 'google', stringifyOptions(options));
	popup?.focus();

	const stuff = await waitForUrl(popup!, redirectUrl);
	console.log(stuff);
	// TODO Exchange token
}

function waitForUrl(popup: Window, redirectUri: string): Promise<unknown> {
	return new Promise((resolve, reject) => {
		const polling = setInterval(() => {
			if (!popup || popup.closed || popup.closed === undefined) {
				clearInterval(polling);
				reject(new Error('The popup window was closed'));
			}

			try {
				console.log(popup?.location.href, redirectUri);
				if (popup?.location.href === redirectUri) {
					if (popup.location.search || popup.location.hash) {
						const query = new URLSearchParams(
							popup.location.search.substring(1).replace(/\/$/, '')
						);
						const hash = new URLSearchParams(
							popup.location.hash.substring(1).replace(/[/$]/, '')
						);
						const params = Object.assign({}, query, hash) as object;

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
