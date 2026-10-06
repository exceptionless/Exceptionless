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

const scopeError = 'Scopes not allowed for this application: stacks:write, offline_access. Restart authorization with scopes allowed for this application.';

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

    it('ApproveAuthorization_DuplicateSubmission_SendsOneRequest', async () => {
        // Arrange
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

        // Act
        await fireEvent.click(approve);
        await fireEvent.click(approve);

        // Assert
        expect(approve).toBeDisabled();
        expect(screen.getByRole('checkbox', { name: /Offline Access/ })).toBeDisabled();
        expect(screen.getByRole('checkbox', { name: 'Test Organization' })).toBeDisabled();
        await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
        complete(jsonResponse({ error: 'invalid_scope', error_description: scopeError }, 400));
        expect(await screen.findByText(scopeError)).toBeVisible();
        expect(fetch).toHaveBeenCalledTimes(2);
    });

    it('ApproveAuthorization_FailedRequest_ShowsOAuthDescription', async () => {
        // Arrange
        const fetch = vi
            .fn()
            .mockResolvedValueOnce(consentResponse())
            .mockResolvedValueOnce(jsonResponse({ error: 'invalid_scope', error_description: scopeError }, 400));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        render(AuthorizePage);
        await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Approve' }));

        // Assert
        expect(await screen.findByText(scopeError)).toBeVisible();
        expect(fetch).toHaveBeenCalledTimes(2);
    });

    it('ApproveAuthorization_ValidSelection_SubmitsOnlySelectedScopes', async () => {
        // Arrange
        const fetch = vi
            .fn()
            .mockResolvedValueOnce(consentResponse())
            .mockResolvedValueOnce(jsonResponse({ error: 'invalid_scope', error_description: scopeError }, 400));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        render(AuthorizePage);
        const approve = screen.getByRole('button', { name: 'Approve' });
        await waitFor(() => expect(approve).toBeEnabled());
        const organization = screen.getByRole('checkbox', { name: 'Test Organization' });

        // Act: removing the last organization must leave its selection editable.
        await fireEvent.click(organization);

        // Assert
        expect(approve).toBeDisabled();
        expect(organization).toBeEnabled();

        // Act: restore membership selection and decline optional offline access.
        await fireEvent.click(organization);
        await fireEvent.click(screen.getByRole('checkbox', { name: /Offline Access/ }));
        await fireEvent.click(approve);

        // Assert
        await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
        const request = fetch.mock.calls[1]![0] as Request;
        const body = await request.json();
        expect(body.scope).toBe('mcp:read');
        expect(body.organization_ids).toEqual(['organization-1']);
    });

    it('CancelAuthorization_ValidatedRequest_DoesNotSendAuthorizationRequest', async () => {
        // Arrange
        const fetch = vi.fn().mockResolvedValueOnce(consentResponse());
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));
        render(AuthorizePage);
        await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

        // Assert
        expect(await screen.findByText('Authorization canceled. You can close this tab.')).toBeVisible();
        expect(fetch).toHaveBeenCalledOnce();
    });

    it('LoadConsent_FailedRequest_DisablesSelectionsAndApproval', async () => {
        // Arrange
        const fetch = vi.fn().mockResolvedValue(jsonResponse({ error: 'invalid_scope', error_description: scopeError }, 400));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));

        // Act
        render(AuthorizePage);

        // Assert
        expect(await screen.findByText(scopeError)).toBeVisible();
        expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
        expect(screen.getAllByText('Required')).toHaveLength(1);
        expect(screen.getByRole('checkbox', { name: /Stacks Write/ })).toBeDisabled();
        expect(screen.getByRole('checkbox', { name: /Offline Access/ })).toBeDisabled();
        expect(screen.getByRole('checkbox', { name: 'Test Organization' })).toBeDisabled();
        expect(fetch).toHaveBeenCalledOnce();
    });

    it('LoadConsent_PendingRequest_DisablesSelectionsUntilValidated', async () => {
        // Arrange
        let complete: (response: Response) => void = () => {};
        const fetch = vi.fn().mockImplementation(
            () =>
                new Promise<Response>((resolve) => {
                    complete = resolve;
                })
        );
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));

        // Act
        render(AuthorizePage);
        await waitFor(() => expect(fetch).toHaveBeenCalledOnce());

        // Assert
        expect(screen.getByRole('checkbox', { name: /Offline Access/ })).toBeDisabled();
        expect(screen.getByRole('checkbox', { name: 'Test Organization' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();

        // Act
        complete(consentResponse());

        // Assert
        await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());
        expect(screen.getByRole('checkbox', { name: /Offline Access/ })).toBeEnabled();
        expect(screen.getByRole('checkbox', { name: 'Test Organization' })).toBeEnabled();
    });

    it('LoadConsent_UntrustedDescription_RendersText', async () => {
        // Arrange
        const description = '<img src=x onerror=alert(1)>';
        const fetch = vi.fn().mockResolvedValue(jsonResponse({ error_description: description }, 400));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));

        // Act
        const { container } = render(AuthorizePage);

        // Assert
        expect(await screen.findByText(description)).toBeVisible();
        expect(container.querySelector('img[src="x"]')).toBeNull();
        expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    });

    it.each(['consent', 'approval'])('Session_ExpiredDuring%s_RedirectsWithOriginalQuery', async (stage) => {
        // Arrange
        const fetch = vi.fn();
        if (stage === 'approval') {
            fetch.mockResolvedValueOnce(consentResponse());
        }
        fetch.mockResolvedValueOnce(jsonResponse({}, 401));
        mocks.useFetchClient.mockReturnValue(new FetchClient({ baseUrl: 'http://localhost/api/v2/', fetch }));

        // Act
        render(AuthorizePage);
        if (stage === 'approval') {
            await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled());
            await fireEvent.click(screen.getByRole('button', { name: 'Approve' }));
        }

        // Assert
        await waitFor(() => expect(mocks.clearSession).toHaveBeenCalledOnce());
        expect(mocks.goto).toHaveBeenCalledWith(`/login?redirect=${encodeURIComponent(mocks.page.url.pathname + mocks.page.url.search)}`, {
            replaceState: true
        });
    });
});
