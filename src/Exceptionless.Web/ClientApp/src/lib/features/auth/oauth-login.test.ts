import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { accessToken, goto, postJSON, useFetchClient } = vi.hoisted(() => ({
    accessToken: { current: null as null | string },
    goto: vi.fn(),
    postJSON: vi.fn(),
    useFetchClient: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$app/state', () => ({ page: {} }));
vi.mock('$env/dynamic/public', () => ({ env: { PUBLIC_GITHUB_APPID: 'github-client-id' } }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({ ...(await importOriginal<object>()), useFetchClient }));
vi.mock('./api.svelte', () => ({}));
vi.mock('./state.svelte', () => ({ accessToken }));
vi.mock('./validators', () => ({ validateEmailAvailability: vi.fn() }));

import { githubLogin } from './index.svelte';

describe('oauth login', () => {
    const popup = { close: vi.fn(), closed: false, focus: vi.fn(), location: new URL('https://github.com/') };

    beforeEach(() => {
        vi.useFakeTimers();
        vi.clearAllMocks();
        accessToken.current = 'existing-session';
        popup.location = new URL('https://github.com/');
        postJSON.mockResolvedValue({ data: { token: 'session-token' }, ok: true });
        useFetchClient.mockReturnValue({ postJSON });
        vi.stubGlobal('window', {
            location: new URL('http://localhost:7131/next/login'),
            open: vi.fn().mockReturnValue(popup),
            outerHeight: 900,
            outerWidth: 1200,
            screenX: 0,
            screenY: 0
        });
    });

    afterEach(() => {
        vi.useRealTimers();
        vi.unstubAllGlobals();
    });

    it('signs in without sending the existing session', async () => {
        // Arrange
        const login = githubLogin({ inviteToken: 'invitation-token', redirectUrl: '/organization/invited' });

        // Act
        popup.location = new URL('http://localhost:7131/?code=authorization-code');
        await vi.advanceTimersByTimeAsync(500);
        await login;

        // Assert
        const [fetchClientOptions] = useFetchClient.mock.calls[0]! as [{ accessTokenFunc: () => null | string }];
        expect(fetchClientOptions.accessTokenFunc()).toBeNull();
        expect(postJSON).toHaveBeenCalledExactlyOnceWith('auth/github', {
            clientId: 'github-client-id',
            code: 'authorization-code',
            inviteToken: 'invitation-token',
            redirectUri: 'http://localhost:7131',
            state: undefined
        });
        expect(accessToken.current).toBe('session-token');
        expect(goto).toHaveBeenCalledExactlyOnceWith('/organization/invited');
    });

    it('links with the existing session', async () => {
        // Arrange
        const login = githubLogin({ linkToCurrentUser: true });

        // Act
        popup.location = new URL('http://localhost:7131/?code=authorization-code');
        await vi.advanceTimersByTimeAsync(500);
        await login;

        // Assert
        expect(useFetchClient).toHaveBeenCalledExactlyOnceWith(undefined);
        expect(postJSON).toHaveBeenCalledOnce();
    });
});
