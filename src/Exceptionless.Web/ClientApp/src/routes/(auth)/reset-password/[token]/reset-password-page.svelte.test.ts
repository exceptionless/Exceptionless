import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ResetPasswordPage from './+page.svelte';

const mocks = vi.hoisted(() => ({
    cancelResetPassword: vi.fn(),
    goto: vi.fn(),
    toastError: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path.replace('/(auth)', '') }));
vi.mock('$app/state', () => ({ page: { params: { token: 'reset-token' }, url: new URL('http://localhost/reset-password/reset-token?cancel=true') } }));
vi.mock('#features/auth/api.svelte.ts', () => ({ cancelResetPassword: mocks.cancelResetPassword, resetPassword: vi.fn() }));
vi.mock('svelte-sonner', () => ({ toast: { error: mocks.toastError } }));

describe('password reset cancellation', () => {
    beforeEach(() => {
        vi.resetAllMocks();
    });

    it('CancelResetPassword_Conflict_OffersRetryAndNavigatesOnlyAfterSuccess', async () => {
        // Arrange
        mocks.cancelResetPassword.mockResolvedValueOnce({ ok: false, status: 409 }).mockResolvedValueOnce({ ok: true, status: 200 });
        render(ResetPasswordPage);

        // Act
        const retry = await screen.findByRole('button', { name: 'Try Again' });

        // Assert
        expect(mocks.goto).not.toHaveBeenCalled();
        expect(mocks.cancelResetPassword).toHaveBeenCalledExactlyOnceWith('reset-token');
        expect(screen.queryByText('Cancelling password reset...')).not.toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'Back to Login' })).toHaveAttribute('href', '/login');

        // Act
        await fireEvent.click(retry);

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalledWith('/login'));
        expect(mocks.cancelResetPassword).toHaveBeenCalledTimes(2);
    });

    it('CancelResetPassword_NetworkFailure_OffersRetry', async () => {
        // Arrange
        mocks.cancelResetPassword.mockRejectedValueOnce(new Error('Connection lost'));
        render(ResetPasswordPage);

        // Act
        const retry = await screen.findByRole('button', { name: 'Try Again' });

        // Assert
        expect(retry).toBeEnabled();
        expect(mocks.goto).not.toHaveBeenCalled();
        expect(mocks.cancelResetPassword).toHaveBeenCalledOnce();
    });
});
