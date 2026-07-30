import { FetchClient } from '@foundatiofx/fetchclient';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('./exceptionless-session', () => ({
    endSession: vi.fn()
}));

import { login, logout } from './api.svelte';

describe('login', () => {
    it('includes the organization invitation token', async () => {
        const mockClient = {
            isLoading: false,
            postJSON: vi.fn().mockResolvedValue({ data: { token: 'access-token' }, ok: true, status: 200 })
        } as unknown as FetchClient;

        await login('invited@example.com', 'password', 'invite-token', mockClient);

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

describe('logout', () => {
    beforeEach(() => {
        // Mock localStorage for server-side tests
        Object.defineProperty(globalThis, 'localStorage', {
            configurable: true,
            value: {
                removeItem: vi.fn()
            },
            writable: true
        });
    });

    it('uses the provided client instance for the logout request', async () => {
        const mockClient = {
            get: vi.fn().mockResolvedValue({ ok: true, status: 200 }),
            isLoading: false
        } as unknown as FetchClient;

        await logout(undefined, mockClient);

        expect(mockClient.get).toHaveBeenCalledWith('auth/logout', { expectedStatusCodes: [200, 401, 403] });
    });
});
