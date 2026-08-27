import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import LoginPage from './login/+page.svelte';
import SignupPage from './signup/+page.svelte';

const authApi = vi.hoisted(() => ({
    login: vi.fn(),
    signup: vi.fn()
}));
const authUi = vi.hoisted(() => ({
    googleLogin: vi.fn()
}));
const navigation = vi.hoisted(() => ({
    afterNavigate: vi.fn(),
    beforeNavigate: vi.fn(),
    goto: vi.fn(),
    pushState: vi.fn(),
    replaceState: vi.fn()
}));
const queryClient = vi.hoisted(() => ({
    cancelQueries: vi.fn(),
    clear: vi.fn()
}));
const validateEmailAvailability = vi.hoisted(() => vi.fn().mockResolvedValue(undefined));

vi.mock('$app/environment', () => ({ browser: true, building: false, dev: false }));
vi.mock('$app/navigation', () => navigation);
vi.mock('$app/paths', () => ({
    resolve: (route: string) => route
}));
vi.mock('$app/state', () => ({
    page: {
        state: {},
        get url() {
            return new URL(window.location.href);
        }
    }
}));
vi.mock('$features/auth/api.svelte', () => authApi);
vi.mock('$features/auth/index.svelte', () => ({
    enableAccountCreation: false,
    enableOAuthLogin: true,
    facebookClientId: undefined,
    facebookLogin: vi.fn(),
    gitHubClientId: undefined,
    githubLogin: vi.fn(),
    googleClientId: 'google-client-id',
    googleLogin: authUi.googleLogin,
    liveLogin: vi.fn(),
    microsoftClientId: undefined
}));
vi.mock('$features/auth/validators', () => ({
    validateEmailAvailability
}));
vi.mock('@tanstack/svelte-query', () => ({ useQueryClient: () => queryClient }));

describe('invitation query parameters', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        authApi.login.mockResolvedValue({ ok: true });
        authApi.signup.mockResolvedValue({ ok: true });
        navigation.goto.mockResolvedValue(undefined);
        queryClient.cancelQueries.mockResolvedValue(undefined);
        navigation.pushState.mockImplementation((url: string | URL, state: App.PageState) => window.history.pushState(state, '', url));
        navigation.replaceState.mockImplementation((url: string | URL, state: App.PageState) => window.history.replaceState(state, '', url));
    });

    it('login_WhenInvitationTokenChanges_UsesCurrentToken', async () => {
        // Arrange
        const initialToken = 'a'.repeat(40);
        const currentToken = 'b'.repeat(40);
        window.history.replaceState({}, '', `/next/login?token=${initialToken}`);
        render(LoginPage);

        // Act
        window.history.pushState({}, '', `/next/login?token=${currentToken}`);
        window.dispatchEvent(new PopStateEvent('popstate'));

        await fireEvent.input(screen.getByLabelText('Email'), { target: { value: 'invited@example.com' } });
        await fireEvent.input(screen.getByPlaceholderText('Enter password'), { target: { value: 'password' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Login' }));

        // Assert
        await waitFor(() => expect(authApi.login).toHaveBeenCalledWith('invited@example.com', 'password', currentToken));
        expect(queryClient.cancelQueries).toHaveBeenCalledOnce();
        expect(queryClient.clear).toHaveBeenCalledOnce();
        expect(queryClient.clear.mock.invocationCallOrder[0]).toBeLessThan(authApi.login.mock.invocationCallOrder[0]!);
        expect(screen.getByRole('link', { name: 'Signup' }).getAttribute('href')).toBe(`/(auth)/signup?token=${currentToken}`);
    });

    it('signup_WhenInvitationTokenChanges_UsesCurrentToken', async () => {
        // Arrange
        const initialToken = 'a'.repeat(40);
        const currentToken = 'b'.repeat(40);
        window.history.replaceState({}, '', `/next/signup?token=${initialToken}`);
        render(SignupPage);

        // Act
        window.history.pushState({}, '', `/next/signup?token=${currentToken}`);
        window.dispatchEvent(new PopStateEvent('popstate'));

        await fireEvent.click(screen.getByRole('button', { name: 'Sign up with Google' }));

        // Assert
        expect(authUi.googleLogin).toHaveBeenCalledWith('/(app)/project/add', currentToken, queryClient);

        // Act
        await fireEvent.input(screen.getByLabelText('Name'), { target: { value: 'Invited User' } });
        await fireEvent.input(screen.getByLabelText('Email'), { target: { value: 'invited@example.com' } });
        await fireEvent.input(screen.getByLabelText('Password'), { target: { value: 'password' } });
        await waitFor(() => expect(validateEmailAvailability).toHaveBeenCalledWith('invited@example.com'), { timeout: 1500 });
        await fireEvent.click(screen.getByRole('button', { name: 'Create My Account' }));

        // Assert
        await waitFor(() => expect(authApi.signup).toHaveBeenCalledWith('Invited User', 'invited@example.com', 'password', currentToken));
        expect(queryClient.cancelQueries).toHaveBeenCalledOnce();
        expect(queryClient.clear).toHaveBeenCalledOnce();
        expect(queryClient.clear.mock.invocationCallOrder[0]).toBeLessThan(authApi.signup.mock.invocationCallOrder[0]!);
    });
});
