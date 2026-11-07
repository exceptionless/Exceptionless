import { render, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    getJSON: vi.fn(),
    getQueryData: vi.fn(),
    goto: vi.fn(),
    resetQueries: vi.fn(),
    toast: { error: vi.fn(), success: vi.fn(), warning: vi.fn() }
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$app/state', () => ({ page: { url: new URL('https://app.test/account/verify?token=abc') } }));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: () => ({ getJSON: mocks.getJSON }) }));
vi.mock('@tanstack/svelte-query', () => ({
    useQueryClient: () => ({ getQueryData: mocks.getQueryData, resetQueries: mocks.resetQueries })
}));
vi.mock('#features/users/api.svelte.ts', () => ({ queryKeys: { me: () => ['User', 'me'] } }));
vi.mock('svelte-sonner', () => ({ toast: mocks.toast }));

import VerifyPage from './+page.svelte';

describe('account verify page', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mocks.getJSON.mockResolvedValue({ data: { email_address: 'alice@example.com' } });
        mocks.resetQueries.mockResolvedValue(undefined);
    });

    it('confirms verification when the signed-in account is now verified', async () => {
        // Arrange
        mocks.getQueryData.mockReturnValue({ email_address: 'Alice@Example.com', is_email_address_verified: true });

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect(mocks.toast.success).toHaveBeenCalledWith('Your account has been successfully verified.');
        expect(mocks.toast.warning).not.toHaveBeenCalled();
    });

    it('names both accounts when the link verified a different, unverified account', async () => {
        // Arrange
        mocks.getQueryData.mockReturnValue({ email_address: 'bob@example.com', is_email_address_verified: false });

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect(mocks.toast.warning).toHaveBeenCalledWith("Verified alice@example.com. You're signed in as bob@example.com, which still needs verifying.");
        expect(mocks.toast.success).not.toHaveBeenCalled();
    });

    it('names both accounts when the signed-in account was already verified', async () => {
        // Arrange
        mocks.getQueryData.mockReturnValue({ email_address: 'bob@example.com', is_email_address_verified: true });

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect(mocks.toast.warning).toHaveBeenCalledWith("Verified alice@example.com. You're signed in as bob@example.com.");
        expect(mocks.toast.success).not.toHaveBeenCalled();
    });

    it('reports an error when the link cannot be verified', async () => {
        // Arrange
        mocks.getJSON.mockRejectedValue(new Error('Invalid token'));

        // Act
        render(VerifyPage);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalled());
        expect(mocks.toast.error).toHaveBeenCalled();
        expect(mocks.toast.warning).not.toHaveBeenCalled();
    });
});
