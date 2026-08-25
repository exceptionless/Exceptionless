import type { FetchClient } from '@foundatiofx/fetchclient';

import { beforeEach, describe, expect, it, vi } from 'vitest';

const clearAuthenticationSession = vi.hoisted(() => vi.fn());
const organization = vi.hoisted(() => ({ current: undefined as string | undefined }));

vi.mock('./exceptionless-session', () => ({
    endSession: vi.fn()
}));
vi.mock('./session.svelte', () => ({ clearAuthenticationSession }));
vi.mock('$features/organizations/context.svelte', () => ({ organization }));

import { login, logout, signup } from './api.svelte';

describe('login', () => {
    beforeEach(() => {
        organization.current = 'previous-organization';
    });

    it('login_WithInvitationToken_IncludesTokenAndClearsPreviousOrganization', async () => {
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
        expect(organization.current).toBeUndefined();
    });

    it('login_WithoutInvitationToken_PreservesPreviousOrganization', async () => {
        // Arrange
        const mockClient = {
            isLoading: false,
            postJSON: vi.fn().mockResolvedValue({ data: { token: 'access-token' }, ok: true, status: 200 })
        } as unknown as FetchClient;

        // Act
        await login('existing@example.com', 'password', undefined, mockClient);

        // Assert
        expect(organization.current).toBe('previous-organization');
    });
});

describe('signup', () => {
    beforeEach(() => {
        organization.current = 'previous-organization';
    });

    it('signup_WithInvitationToken_ClearsPreviousOrganization', async () => {
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
        expect(organization.current).toBeUndefined();
    });
});

describe('logout', () => {
    beforeEach(() => {
        clearAuthenticationSession.mockReset();
        organization.current = 'previous-organization';
    });

    it('logout_WithProvidedClient_UsesClientAndClearsSession', async () => {
        // Arrange
        const mockClient = {
            get: vi.fn().mockResolvedValue({ ok: true, status: 200 }),
            isLoading: false
        } as unknown as FetchClient;

        // Act
        await logout(undefined, mockClient);

        // Assert
        expect(mockClient.get).toHaveBeenCalledWith('auth/logout', { expectedStatusCodes: [200, 401, 403] });
        expect(clearAuthenticationSession).toHaveBeenCalledOnce();
        expect(organization.current).toBeUndefined();
    });
});
