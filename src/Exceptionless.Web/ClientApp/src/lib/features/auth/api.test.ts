import { FetchClient } from '@exceptionless/fetchclient';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { logout } from './api.svelte';

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
