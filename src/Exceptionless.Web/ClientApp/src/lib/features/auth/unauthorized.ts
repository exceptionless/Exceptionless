import { clearAuthenticationSession } from './session.svelte';
import { accessToken } from './state.svelte';

export function handleUnexpectedUnauthorized(status: number, expectedStatusCodes?: number[]) {
    if (status !== 401 || expectedStatusCodes?.includes(401) || !accessToken.current) {
        return false;
    }

    clearAuthenticationSession('');
    return true;
}
