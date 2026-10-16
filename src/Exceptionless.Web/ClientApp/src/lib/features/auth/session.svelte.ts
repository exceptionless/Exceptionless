import { shutdownIntercomSession } from '#features/intercom/session.ts';

import { accessToken } from './state.svelte';

export function clearAuthenticationSession(clearedAccessToken: '' | null = null) {
    shutdownIntercomSession();
    accessToken.current = clearedAccessToken;
}
