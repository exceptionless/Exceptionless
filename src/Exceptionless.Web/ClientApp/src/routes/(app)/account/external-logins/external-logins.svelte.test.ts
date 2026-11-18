import { fireEvent, render, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { googleLogin } = vi.hoisted(() => ({
    googleLogin: vi.fn()
}));

import ExternalLogins from './+page.svelte';

vi.mock('$features/auth/index.svelte', () => ({
    enableOAuthLogin: true,
    facebookClientId: '',
    facebookLogin: vi.fn(),
    gitHubClientId: '',
    githubLogin: vi.fn(),
    googleClientId: 'google-client-id',
    googleLogin,
    liveLogin: vi.fn(),
    microsoftClientId: ''
}));
vi.mock('$features/auth/api.svelte', () => ({ unlinkOAuthAccount: vi.fn() }));
vi.mock('$features/users/api.svelte', () => ({
    getMeQuery: () => ({
        data: {
            has_local_account: true,
            o_auth_accounts: []
        }
    })
}));

describe('external logins', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('links the provider to the signed-in user', async () => {
        // Arrange
        render(ExternalLogins);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Link Google account' }));

        // Assert
        expect(googleLogin).toHaveBeenCalledExactlyOnceWith({ linkToCurrentUser: true });
    });
});
