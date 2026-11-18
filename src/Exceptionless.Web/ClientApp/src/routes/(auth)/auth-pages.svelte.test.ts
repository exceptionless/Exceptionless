import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { testAccessToken } from './auth-session.test-harness.svelte';
import LoginPage from './login/+page.svelte';
import SignupPage from './signup/+page.svelte';

const mocks = vi.hoisted(() => ({
    clearAuthenticationSession: vi.fn(),
    gotoRedirect: vi.fn(),
    logout: vi.fn(),
    page: { url: new URL('http://localhost/login') }
}));

vi.mock('$app/env', () => ({ browser: true, dev: false }));
vi.mock('$app/state', () => ({ page: mocks.page }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path.replace('/(auth)', '').replace('/(app)', '') || '/' }));
vi.mock('#features/navigation/redirect.ts', () => ({ gotoRedirect: mocks.gotoRedirect }));
vi.mock('#features/auth/session.svelte.ts', () => ({ clearAuthenticationSession: mocks.clearAuthenticationSession }));
vi.mock('#features/auth/api.svelte.ts', () => ({ login: vi.fn(), signup: vi.fn() }));
vi.mock('#features/auth/index.svelte.ts', async () => {
    const { testAccessToken } = await import('./auth-session.test-harness.svelte');
    return {
        accessToken: testAccessToken,
        enableAccountCreation: true,
        enableOAuthLogin: false,
        gitHubClientId: undefined,
        githubLogin: vi.fn(),
        googleClientId: undefined,
        googleLogin: vi.fn(),
        logout: mocks.logout,
        microsoftClientId: undefined,
        microsoftLogin: vi.fn(),
        validateEmailAvailability: vi.fn()
    };
});
vi.mock('#features/users/api.svelte.ts', () => ({
    getMeQuery: () => ({ data: { email_address: 'current@example.test' }, isError: false })
}));
vi.mock('@tanstack/svelte-query', () => ({ useQueryClient: () => ({ cancelQueries: vi.fn(), clear: vi.fn() }) }));

const pages = [
    { component: LoginPage, formHeading: 'Welcome back', otherLink: 'Create an account', path: '/login' },
    { component: SignupPage, formHeading: 'Create your account', otherLink: 'Log in', path: '/signup' }
] as const;

describe.each(pages)('$path with an existing session', ({ component, formHeading, otherLink, path }) => {
    beforeEach(() => {
        vi.clearAllMocks();
        testAccessToken.current = 'existing-session';
        mocks.logout.mockImplementation(async () => {
            testAccessToken.current = null;
        });
        mocks.clearAuthenticationSession.mockImplementation(() => {
            testAccessToken.current = null;
        });
    });

    it('continues into the app when there is no invitation', async () => {
        // Arrange
        mocks.page.url = new URL(`http://localhost${path}?redirect=%2Fevent`);

        // Act
        render(component);

        // Assert
        await waitFor(() => expect(mocks.gotoRedirect).toHaveBeenCalledWith('/event', { replace: true }));
        expect(mocks.logout).not.toHaveBeenCalled();
        expect(screen.queryByRole('heading', { name: formHeading })).toBeNull();
    });

    it('keeps the session when continuing without accepting an invitation', () => {
        // Arrange
        mocks.page.url = new URL(`http://localhost${path}?token=invite-token`);

        // Act
        render(component);

        // Assert
        expect(screen.getByText("You're signed in as current@example.test")).toBeVisible();
        expect(screen.getByRole('link', { name: 'Continue as current@example.test without accepting' })).toHaveAttribute('href', '/');
        expect(mocks.logout).not.toHaveBeenCalled();
        expect(mocks.gotoRedirect).not.toHaveBeenCalled();
    });

    it.each([
        ['signs out', () => undefined],
        ['clears the local session when sign-out fails', () => mocks.logout.mockRejectedValueOnce(new Error('offline'))]
    ])('%s and shows the form with the invitation kept', async (_name, arrange) => {
        // Arrange
        arrange();
        mocks.page.url = new URL(`http://localhost${path}?token=invite-token`);
        render(component);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Sign out and accept invitation' }));

        // Assert
        expect(await screen.findByRole('heading', { name: formHeading })).toBeVisible();
        expect(screen.getByRole('link', { name: otherLink }).getAttribute('href')).toContain('token=invite-token');
        expect(testAccessToken.current).toBeNull();
    });
});
