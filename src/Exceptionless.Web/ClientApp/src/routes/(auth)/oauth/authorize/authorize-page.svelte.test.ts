import { FetchClient } from '@foundatiofx/fetchclient';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AuthorizePage from './+page.svelte';

const mocks = vi.hoisted(() => ({
    clearSession: vi.fn(),
    goto: vi.fn(),
    page: { url: new URL('http://localhost/oauth/authorize') },
    useFetchClient: vi.fn()
}));

vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path.replace('/(auth)', '') }));
vi.mock('$app/state', () => ({ page: mocks.page }));
vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'test-token' } }));
vi.mock('$features/auth/session.svelte', () => ({ clearAuthenticationSession: mocks.clearSession }));
vi.mock('$features/organizations/api.svelte', () => ({
    getOrganizationsQuery: () => ({ data: { data: [{ id: 'organization-1', name: 'Test Organization' }] }, isError: false, isLoading: false })
}));
vi.mock('$features/users/api.svelte', () => ({
    getMeQuery: () => ({ data: { email_address: 'member@example.test', full_name: 'Member' }, isError: false, isLoading: false })
}));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: mocks.useFetchClient
}));

const scopeError =
    'Scopes not allowed for this application: stacks:write, offline_access. Restart authorization with fewer scopes or ask a global administrator to review the application in System → OAuth Apps.';

function consentResponse() {
    return jsonResponse({ client_id: 'test-client', client_name: 'Test Client', required_scopes: ['mcp:read'], scopes: ['mcp:read', 'offline_access'] });
}

function jsonResponse(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), { headers: { 'Content-Type': 'application/json' }, status });
}

describe('OAuth authorization', () => {
    beforeEach(() => {
        mocks.page.url = new URL(
            'http://localhost/oauth/authorize?client_id=test-client&redirect_uri=http://localhost/callback&resource=http://localhost/mcp&response_type=code&code_challenge=test&code_challenge_method=S256&scope=mcp:read+offline_access+stacks:write'
        );
    });

    it('shows the actual FetchClient OAuth error on failed consent and keeps approval disabled after scope edits', async () => {
        const fetch = vi.fn().mockResolvedValue(jsonResponse({ error: 'invalid_scope', error_description: scopeError }, 400));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        render(AuthorizePage);

        expect(await screen.findByText(scopeError)).toBeVisible();
        expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
        expect(screen.getAllByText('Required')).toHaveLength(1);
        await fireEvent.click(screen.getByRole('checkbox', { name: /Stacks Write/ }));
        expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
        expect(fetch).toHaveBeenCalledOnce();
    });

    it('shows the actual FetchClient OAuth error when final authorization fails', async () => {
        const fetch = vi
            .fn()
            .mockResolvedValueOnce(consentResponse())
            .mockResolvedValueOnce(jsonResponse({ error: 'invalid_scope', error_description: scopeError }, 400));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        render(AuthorizePage);
        await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());
        await fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

        expect(await screen.findByText(scopeError)).toBeVisible();
        expect(fetch).toHaveBeenCalledTimes(2);
    });

    it.each(['consent', 'approval'])('returns to login with the original query after session expiry during %s', async (stage) => {
        const fetch = vi.fn();
        if (stage === 'approval') {
            fetch.mockResolvedValueOnce(consentResponse());
        }

        fetch.mockResolvedValueOnce(jsonResponse({}, 401));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        render(AuthorizePage);
        if (stage === 'approval') {
            await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());
            await fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
        }

        await waitFor(() => expect(mocks.clearSession).toHaveBeenCalledOnce());
        expect(mocks.goto).toHaveBeenCalledWith(`/login?redirect=${encodeURIComponent(mocks.page.url.pathname + mocks.page.url.search)}`, {
            replaceState: true
        });
    });

    it('sends one authorization request while approval is pending and cancel sends none', async () => {
        let complete: (response: Response) => void = () => {};
        const fetch = vi
            .fn()
            .mockResolvedValueOnce(consentResponse())
            .mockImplementationOnce(
                () =>
                    new Promise<Response>((resolve) => {
                        complete = resolve;
                    })
            );
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        render(AuthorizePage);
        const approve = screen.getByRole('button', { name: 'Approve' });
        await waitFor(() => expect(approve).toBeEnabled());
        await fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
        expect(await screen.findByText('Authorization canceled. You can close this tab.')).toBeVisible();
        expect(fetch).toHaveBeenCalledOnce();
        await fireEvent.click(approve);
        await fireEvent.click(approve);
        expect(approve).toBeDisabled();
        await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
        complete(jsonResponse({ error: 'invalid_scope', error_description: scopeError }, 400));
        expect(await screen.findByText(scopeError)).toBeVisible();
        expect(fetch).toHaveBeenCalledTimes(2);
    });

    it('renders an error as text without creating HTML', async () => {
        const description = '<img src=x onerror=alert(1)>';
        const fetch = vi.fn().mockResolvedValue(jsonResponse({ error_description: description }, 400));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        const { container } = render(AuthorizePage);
        expect(await screen.findByText(description)).toBeVisible();
        expect(container.querySelector('img[src="x"]')).toBeNull();
        expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    });
});
