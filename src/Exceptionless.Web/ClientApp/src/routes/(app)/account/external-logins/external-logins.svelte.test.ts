import { render, screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const state = vi.hoisted(() => ({
    enableOAuthLogin: false,
    hasLocalAccount: true,
    oauthAccounts: [{ provider: 'facebook', provider_user_id: 'legacy-facebook-id', username: 'legacy-user' }]
}));

import ExternalLogins from './+page.svelte';

vi.mock('#features/auth/index.svelte.ts', () => ({
    get enableOAuthLogin() {
        return state.enableOAuthLogin;
    },
    gitHubClientId: '',
    githubLogin: vi.fn(),
    get googleClientId() {
        return state.enableOAuthLogin ? 'google-client-id' : '';
    },
    googleLogin: vi.fn(),
    microsoftClientId: '',
    microsoftLogin: vi.fn()
}));
vi.mock('#features/auth/api.svelte.ts', () => ({ unlinkOAuthAccount: vi.fn() }));
vi.mock('#features/users/api.svelte.ts', () => ({
    getMeQuery: () => ({
        data: {
            has_local_account: state.hasLocalAccount,
            o_auth_accounts: state.oauthAccounts
        }
    })
}));

describe('external logins', () => {
    beforeEach(() => {
        state.enableOAuthLogin = false;
        state.hasLocalAccount = true;
        state.oauthAccounts = [{ provider: 'facebook', provider_user_id: 'legacy-facebook-id', username: 'legacy-user' }];
    });

    it('hides external logins when no providers are configured', () => {
        render(ExternalLogins);

        expect(screen.getByText('External login providers are not configured.')).toBeVisible();
        expect(screen.queryByRole('button', { name: /Link .* account/ })).not.toBeInTheDocument();
        expect(screen.queryByText('Existing external logins')).not.toBeInTheDocument();
        expect(screen.queryByText('facebook (legacy-user)')).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'Remove facebook account' })).not.toBeInTheDocument();
    });

    it.each(['facebook', 'WindowsLive'])('protects the last active login when a retained %s account exists', (retiredProvider) => {
        state.enableOAuthLogin = true;
        state.hasLocalAccount = false;
        state.oauthAccounts = [
            { provider: 'google', provider_user_id: 'google-user', username: 'user@example.com' },
            { provider: retiredProvider, provider_user_id: 'retired-user', username: 'user@example.com' }
        ];

        render(ExternalLogins);

        expect(screen.getByRole('button', { name: 'Remove google account' })).toBeDisabled();
        expect(screen.getByRole('button', { name: `Remove ${retiredProvider} account` })).toBeEnabled();
    });

    it.each([true, false])('allows unlinking when another login remains, with local account: %s', (hasLocalAccount) => {
        state.enableOAuthLogin = true;
        state.hasLocalAccount = hasLocalAccount;
        state.oauthAccounts = [{ provider: 'google', provider_user_id: 'google-user', username: 'user@example.com' }];
        if (!hasLocalAccount) {
            state.oauthAccounts.push({ provider: 'microsoft', provider_user_id: 'microsoft-user', username: 'user@example.com' });
        }

        render(ExternalLogins);

        expect(screen.getByRole('button', { name: 'Remove google account' })).toBeEnabled();
    });
});
