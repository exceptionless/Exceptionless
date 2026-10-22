import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import ExternalLogins from './+page.svelte';

vi.mock('#features/auth/index.svelte.ts', () => ({
    enableOAuthLogin: false,
    gitHubClientId: '',
    githubLogin: vi.fn(),
    googleClientId: '',
    googleLogin: vi.fn(),
    microsoftClientId: '',
    microsoftLogin: vi.fn()
}));
vi.mock('#features/auth/api.svelte.ts', () => ({ unlinkOAuthAccount: vi.fn() }));
vi.mock('#features/users/api.svelte.ts', () => ({
    getMeQuery: () => ({
        data: {
            has_local_account: true,
            o_auth_accounts: [{ provider: 'facebook', provider_user_id: 'legacy-facebook-id', username: 'legacy-user' }]
        }
    })
}));

describe('external logins', () => {
    it('keeps legacy accounts available to unlink when no providers are configured', () => {
        render(ExternalLogins);

        expect(screen.getByText('External login providers are not configured.')).toBeVisible();
        expect(screen.queryByRole('button', { name: /Link .* account/ })).not.toBeInTheDocument();
        expect(screen.getByText('facebook (legacy-user)')).toBeVisible();
        expect(screen.getByRole('button', { name: 'Remove facebook account' })).toBeEnabled();
    });
});
