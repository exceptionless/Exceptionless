import { FetchClient } from '@foundatiofx/fetchclient';
import { QueryClient } from '@tanstack/svelte-query';
import { render, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, type Mock, type MockInstance, vi } from 'vitest';

import { queryKeys } from '#features/users/api.svelte.ts';

import VerifyPage from './+page.svelte';

const mocks = vi.hoisted(() => ({
    goto: vi.fn(),
    page: { url: new URL('http://localhost/account/verify') },
    queryClient: undefined as QueryClient | undefined,
    toast: { error: vi.fn(), success: vi.fn() },
    useFetchClient: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path.replace('/(app)', '') }));
vi.mock('$app/state', () => ({ page: mocks.page }));
vi.mock('svelte-sonner', () => ({ toast: mocks.toast }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: mocks.useFetchClient
}));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    useQueryClient: () => mocks.queryClient
}));

const token = 'A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8S9t0';

function problemResponse(status: number, title: string) {
    return new Response(JSON.stringify({ status, title }), { headers: { 'Content-Type': 'application/problem+json' }, status });
}

describe('Verify email address page', () => {
    let fetch: Mock<typeof globalThis.fetch>;
    let resetQueries: MockInstance<QueryClient['resetQueries']>;

    beforeEach(() => {
        vi.clearAllMocks();
        fetch = vi.fn<typeof globalThis.fetch>();
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        mocks.queryClient = new QueryClient();
        resetQueries = vi.spyOn(mocks.queryClient, 'resetQueries');
        mocks.page.url = new URL(`http://localhost/account/verify?token=${token}`);
    });

    it('VerifyEmailAddress_ValidToken_VerifiesWithGetAndResetsCurrentUser', async () => {
        // Arrange
        fetch.mockResolvedValue(Response.json({ email_address: 'alice@example.com' }));

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalledWith('/account/manage'));
        expect(fetch).toHaveBeenCalledOnce();
        const request = fetch.mock.calls[0]![0] as Request;
        expect(request.url).toBe(`http://localhost/api/v2/users/verify-email-address/${token}`);
        expect(request.method).toBe('GET');
        expect(resetQueries).toHaveBeenCalledWith({ exact: true, queryKey: queryKeys.me() });
        // The link may belong to a different account than the one signed in, so name the address it verified.
        expect(mocks.toast.success).toHaveBeenCalledWith('Verified alice@example.com.');
        expect(mocks.toast.error).not.toHaveBeenCalled();
    });

    it.each([
        [404, 'User not found.', 'This verification link is invalid or has already been used.'],
        [422, 'Verify Email Address Token has expired.', 'This verification link has expired.'],
        [500, 'Internal Server Error', 'We could not verify your email address.']
    ])('VerifyEmailAddress_%i_ShowsSpecificErrorWithoutRefreshingCurrentUser', async (status, title, message) => {
        // Arrange
        fetch.mockResolvedValue(problemResponse(status, title));

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalledWith('/account/manage'));
        expect(mocks.toast.error).toHaveBeenCalledWith(expect.stringContaining(message));
        expect(mocks.toast.success).not.toHaveBeenCalled();
        expect(resetQueries).not.toHaveBeenCalled();
    });

    it('VerifyEmailAddress_MissingToken_ExplainsWithoutCallingApi', async () => {
        // Arrange
        mocks.page.url = new URL('http://localhost/account/verify');

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalledWith('/account/manage'));
        expect(fetch).not.toHaveBeenCalled();
        expect(mocks.toast.error).toHaveBeenCalledWith(expect.stringContaining('This verification link is incomplete.'));
    });

    it('VerifyEmailAddress_TokenWithReservedCharacters_EncodesPathSegment', async () => {
        // Arrange
        mocks.page.url = new URL('http://localhost/account/verify?token=abc%2F..%3Fx%3D1');
        fetch.mockResolvedValue(problemResponse(404, 'Not Found'));

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect((fetch.mock.calls[0]![0] as Request).url).toBe('http://localhost/api/v2/users/verify-email-address/abc%2F..%3Fx%3D1');
    });
});
