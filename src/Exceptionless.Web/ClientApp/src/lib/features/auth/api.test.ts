import type { FetchClient } from '@foundatiofx/fetchclient';
import type { QueryClient } from '@tanstack/svelte-query';

import { beforeEach, describe, expect, it, vi } from 'vitest';

const clearAuthenticationSession = vi.hoisted(() => vi.fn());

vi.mock('./exceptionless-session', () => ({
    endSession: vi.fn()
}));
vi.mock('./session.svelte', () => ({ clearAuthenticationSession }));

import { login, logout, signup } from './api.svelte';

describe('login', () => {
    it('login_WithInvitationToken_IncludesToken', async () => {
        // Arrange
        const mockClient = {
            isLoading: false,
            postJSON: vi.fn().mockResolvedValue({ data: { token: 'access-token' }, ok: true, status: 200 })
        } as unknown as FetchClient;

        // Act
        await login('invited@example.com', 'password', 'invite-token', mockClient);

        // Assert
        expect(mockClient.postJSON).toHaveBeenCalledWith(
            'auth/login',
            {
                email: 'invited@example.com',
                invite_token: 'invite-token',
                password: 'password'
            },
            { expectedStatusCodes: [401, 422] }
        );
    });
});

describe('signup', () => {
    it('signup_WithInvitationToken_IncludesToken', async () => {
        // Arrange
        const mockClient = {
            isLoading: false,
            postJSON: vi.fn().mockResolvedValue({ data: { token: 'access-token' }, ok: true, status: 200 })
        } as unknown as FetchClient;

        // Act
        await signup('Invited User', 'invited@example.com', 'password', 'invite-token', mockClient);

        // Assert
        expect(mockClient.postJSON).toHaveBeenCalledWith(
            'auth/signup',
            {
                email: 'invited@example.com',
                invite_token: 'invite-token',
                name: 'Invited User',
                password: 'password'
            },
            { expectedStatusCodes: [401, 403, 422] }
        );
    });
});

describe('logout', () => {
    beforeEach(() => {
        clearAuthenticationSession.mockReset();
        Object.defineProperty(globalThis, 'localStorage', {
            configurable: true,
            value: {
                removeItem: vi.fn()
            },
            writable: true
        });
    });

    it('logout_WithProvidedClients_ClearsQueriesAndSession', async () => {
        // Arrange
        const mockClient = {
            get: vi.fn().mockResolvedValue({ ok: true, status: 200 }),
            isLoading: false
        } as unknown as FetchClient;
        const queryClient = {
            cancelQueries: vi.fn().mockResolvedValue(undefined),
            clear: vi.fn()
        } as unknown as QueryClient;

        // Act
        await logout(queryClient, mockClient);

        // Assert
        expect(mockClient.get).toHaveBeenCalledWith('auth/logout', { expectedStatusCodes: [200, 401, 403] });
        expect(queryClient.cancelQueries).toHaveBeenCalledOnce();
        expect(queryClient.clear).toHaveBeenCalledOnce();
        expect(clearAuthenticationSession).toHaveBeenCalledOnce();
    });
});
