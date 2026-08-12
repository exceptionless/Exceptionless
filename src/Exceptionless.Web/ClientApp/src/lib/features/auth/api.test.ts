import { FetchClient } from '@foundatiofx/fetchclient';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const clearAuthenticationSession = vi.hoisted(() => vi.fn());

vi.mock('./exceptionless-session', () => ({
    endSession: vi.fn()
}));
vi.mock('./session.svelte', () => ({ clearAuthenticationSession }));

import { logout } from './api.svelte';

describe('logout', () => {
    beforeEach(() => {
        clearAuthenticationSession.mockReset();
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
        expect(clearAuthenticationSession).toHaveBeenCalledOnce();
    });
});
