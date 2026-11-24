import { FetchClient } from '@foundatiofx/fetchclient';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const clearAuthenticationSession = vi.hoisted(() => vi.fn());
const useFetchClient = vi.hoisted(() => vi.fn());

vi.mock('./exceptionless-session', () => ({
    endSession: vi.fn()
}));
vi.mock('./session.svelte', () => ({ clearAuthenticationSession }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient
}));

import { login, logout, signup } from './api.svelte';

function unauthorized(title: string) {
    return new Response(JSON.stringify({ status: 401, title }), { headers: { 'Content-Type': 'application/problem+json' }, status: 401 });
}

describe.each([
    ['login', () => login('user@exceptionless.test', 'wrong-password')],
    ['signup', () => signup('User', 'user@exceptionless.test', 'wrong-password')]
])('%s unauthorized messages', (operation, submit) => {
    it.each([
        ['Login denied.', 'Too many attempts. Wait a few minutes and try again, or reset your password.'],
        ['Signup denied.', 'Too many sign up attempts. Wait a while and try again.'],
        [
            'Login failed.',
            operation === 'signup'
                ? 'An account with this email address already exists. Log in, or reset your password if you forgot it.'
                : 'Invalid email or password'
        ],
        ['Signup failed.', 'Invalid email or password']
    ])('maps %s to a form message', async (title, message) => {
        // Arrange
        useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch: vi.fn().mockResolvedValue(unauthorized(title)) }));

        // Act
        const response = await submit();

        // Assert
        expect(response.status).toBe(401);
        expect(response.problem.errors.general).toEqual([message]);
    });
});

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
