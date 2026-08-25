import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const authApi = vi.hoisted(() => ({
    cancelResetPassword: vi.fn(),
    changePassword: vi.fn(),
    forgotPassword: vi.fn(),
    getIntercomTokenQuery: vi.fn(),
    isEmailAddressTaken: vi.fn(),
    login: vi.fn(),
    logout: vi.fn(),
    resetPassword: vi.fn(),
    signup: vi.fn(),
    unlinkOAuthAccount: vi.fn()
}));
const authState = vi.hoisted(() => ({
    accessToken: { current: 'existing-access-token' as null | string }
}));
const fetchClient = vi.hoisted(() => ({
    postJSON: vi.fn()
}));
const navigation = vi.hoisted(() => ({
    goto: vi.fn()
}));

vi.mock('$app/navigation', () => navigation);
vi.mock('$app/paths', () => ({ resolve: (route: string) => route }));
vi.mock('$app/state', () => ({ page: { url: new URL('https://app.example.test/next/login') } }));
vi.mock('$env/dynamic/public', () => ({
    env: {
        PUBLIC_ENABLE_ACCOUNT_CREATION: 'true',
        PUBLIC_FACEBOOK_APPID: 'facebook-client-id'
    }
}));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: () => fetchClient }));
vi.mock('./api.svelte', () => authApi);
vi.mock('./state.svelte', () => authState);
vi.mock('./validators', () => ({ validateEmailAvailability: vi.fn() }));

import { facebookLogin } from './index.svelte';

function createOAuthPopup(code: string): Window {
    return {
        close: vi.fn(),
        closed: false,
        focus: vi.fn(),
        location: {
            hash: '',
            href: `${window.location.origin}/?code=${code}`,
            search: `?code=${code}`
        }
    } as unknown as Window;
}

describe('facebookLogin', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        authState.accessToken.current = 'existing-access-token';
        authApi.logout.mockImplementation(async () => {
            authState.accessToken.current = null;
        });
        fetchClient.postJSON.mockResolvedValue({ data: { token: 'new-access-token' }, ok: true });
        navigation.goto.mockResolvedValue(undefined);
    });

    afterEach(() => {
        vi.useRealTimers();
        vi.restoreAllMocks();
    });

    it('facebookLogin_WithInvitationToken_LogsOutBeforeExchangingCode', async () => {
        // Arrange
        const open = vi.spyOn(window, 'open').mockReturnValue(createOAuthPopup('facebook-code'));

        // Act
        const login = facebookLogin('/(app)/project/add', 'invite-token');
        await vi.advanceTimersByTimeAsync(500);
        await login;

        // Assert
        expect(authApi.logout).toHaveBeenCalledOnce();
        expect(open.mock.invocationCallOrder[0]).toBeLessThan(authApi.logout.mock.invocationCallOrder[0]!);
        expect(authApi.logout.mock.invocationCallOrder[0]).toBeLessThan(fetchClient.postJSON.mock.invocationCallOrder[0]!);
        expect(fetchClient.postJSON).toHaveBeenCalledWith('auth/facebook', {
            clientId: 'facebook-client-id',
            code: 'facebook-code',
            inviteToken: 'invite-token',
            redirectUri: window.location.origin,
            state: undefined
        });
        expect(authState.accessToken.current).toBe('new-access-token');
        expect(navigation.goto).toHaveBeenCalledWith('/(app)/project/add');
    });

    it('facebookLogin_WithoutInvitationToken_PreservesAccountLinkingSession', async () => {
        // Arrange
        vi.spyOn(window, 'open').mockReturnValue(createOAuthPopup('facebook-link-code'));

        // Act
        const login = facebookLogin();
        await vi.advanceTimersByTimeAsync(500);
        await login;

        // Assert
        expect(authApi.logout).not.toHaveBeenCalled();
        expect(fetchClient.postJSON).toHaveBeenCalledWith('auth/facebook', {
            clientId: 'facebook-client-id',
            code: 'facebook-link-code',
            inviteToken: undefined,
            redirectUri: window.location.origin,
            state: undefined
        });
    });
});
