import { render, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    getJSON: vi.fn(),
    goto: vi.fn(),
    resetQueries: vi.fn(),
    toast: { error: vi.fn(), success: vi.fn() }
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$app/state', () => ({ page: { url: new URL('https://app.test/account/verify?token=abc') } }));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: () => ({ getJSON: mocks.getJSON }) }));
vi.mock('@tanstack/svelte-query', () => ({
    useQueryClient: () => ({ resetQueries: mocks.resetQueries })
}));
vi.mock('#features/users/api.svelte.ts', () => ({ queryKeys: { me: () => ['User', 'me'] } }));
vi.mock('svelte-sonner', () => ({ toast: mocks.toast }));

import VerifyPage from './+page.svelte';

describe('account verify page', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mocks.resetQueries.mockResolvedValue(undefined);
    });

    it('identifies the email address verified by the link', async () => {
        // Arrange
        mocks.getJSON.mockResolvedValue({ data: { email_address: 'alice@example.com' } });

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect(mocks.toast.success).toHaveBeenCalledWith('Verified alice@example.com.');
        expect(mocks.resetQueries).toHaveBeenCalledWith({ exact: true, queryKey: ['User', 'me'] });
    });

    it('reports verification without claiming the signed-in account when no address is returned', async () => {
        // Arrange
        mocks.getJSON.mockResolvedValue({ data: null });

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect(mocks.toast.success).toHaveBeenCalledWith('Email address verified.');
    });

    it('reports an error when the link cannot be verified', async () => {
        // Arrange
        mocks.getJSON.mockRejectedValue(new Error('Invalid token'));

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect(mocks.toast.error).toHaveBeenCalled();
        expect(mocks.toast.success).not.toHaveBeenCalled();
    });
});
