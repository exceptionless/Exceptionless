import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { accessToken, goto, postJSON } = vi.hoisted(() => ({
    accessToken: { current: null as null | string },
    goto: vi.fn(),
    postJSON: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => `/next${path}` }));
vi.mock('$app/state', () => ({ page: {} }));
vi.mock('$env/dynamic/public', () => ({ env: { PUBLIC_MICROSOFT_APPID: 'microsoft-client-id' } }));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: () => ({ postJSON }) }));
vi.mock('./api.svelte', () => ({}));
vi.mock('./state.svelte', () => ({ accessToken }));
vi.mock('./validators', () => ({ validateEmailAvailability: vi.fn() }));

import { microsoftLogin } from './index.svelte';

describe('microsoftLogin', () => {
    const popup = { close: vi.fn(), closed: false, focus: vi.fn(), location: new URL('https://login.microsoftonline.com/') };
    const open = vi.fn<(url: string) => typeof popup>().mockReturnValue(popup);

    beforeEach(() => {
        vi.useFakeTimers();
        vi.clearAllMocks();
        accessToken.current = null;
        popup.location = new URL('https://login.microsoftonline.com/');
        postJSON.mockResolvedValue({ data: { token: 'session-token' }, ok: true });
        vi.stubGlobal('window', { location: new URL('http://localhost:7131/next/login'), open, outerHeight: 900, outerWidth: 1200, screenX: 0, screenY: 0 });
    });

    afterEach(() => {
        vi.useRealTimers();
        vi.unstubAllGlobals();
    });

    it.each([true, false])('validates state and preserves invitation and redirect with randomUUID available: %s', async (hasRandomUUID) => {
        const getRandomValues = vi.fn((bytes: Uint8Array) => bytes.fill(10));
        vi.stubGlobal('crypto', { getRandomValues, randomUUID: hasRandomUUID ? () => 'oauth-state' : undefined });

        const login = microsoftLogin('/next/organization/invited', 'invitation-token');
        const authorizationUrl = new URL(open.mock.calls[0]![0]);
        expect(authorizationUrl.origin + authorizationUrl.pathname).toBe('https://login.microsoftonline.com/common/oauth2/v2.0/authorize');
        expect(authorizationUrl.searchParams.get('client_id')).toBe('microsoft-client-id');
        expect(authorizationUrl.searchParams.get('scope')).toBe('User.Read');
        expect(authorizationUrl.searchParams.get('response_type')).toBe('code');
        expect(authorizationUrl.searchParams.get('redirect_uri')).toBe('http://localhost:7131');
        const state = authorizationUrl.searchParams.get('state');
        expect(state).toBe(hasRandomUUID ? 'oauth-state' : '0a'.repeat(16));

        popup.location = new URL(`http://localhost:7131/?code=authorization-code&state=${state}`);
        await vi.advanceTimersByTimeAsync(500);
        await login;

        expect(postJSON).toHaveBeenCalledExactlyOnceWith('auth/microsoft', {
            clientId: 'microsoft-client-id',
            code: 'authorization-code',
            inviteToken: 'invitation-token',
            redirectUri: 'http://localhost:7131',
            state
        });
        expect(accessToken.current).toBe('session-token');
        expect(goto).toHaveBeenCalledExactlyOnceWith('/next/organization/invited');
        expect(popup.close).toHaveBeenCalledOnce();
    });

    it.each(['state=wrong-state', '', 'error=access_denied'])('does not exchange the code when the callback contains %s', async (query) => {
        vi.stubGlobal('crypto', { randomUUID: () => 'expected-state' });
        const login = microsoftLogin();
        const rejected = expect(login).rejects.toThrow(query.includes('error=') ? 'access_denied' : 'Invalid state');

        popup.location = new URL(`http://localhost:7131/?code=authorization-code&${query}`);
        await vi.advanceTimersByTimeAsync(500);
        await rejected;

        expect(postJSON).not.toHaveBeenCalled();
        expect(accessToken.current).toBeNull();
        expect(goto).not.toHaveBeenCalled();
        expect(popup.close).toHaveBeenCalledOnce();
    });
});
