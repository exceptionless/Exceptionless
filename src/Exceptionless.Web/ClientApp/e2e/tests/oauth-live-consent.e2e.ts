import { createHash, randomBytes } from 'node:crypto';

import type { OAuthApplication } from '../../src/lib/features/admin/models';

import { expect, test } from '../fixtures/e2e-test';
import { getE2EEnvironment } from '../fixtures/environment';
import { runCleanupStep, throwIfCleanupFailed } from '../support/cleanup';

interface OAuthTokens {
    access_token: string;
    refresh_token?: string;
    scope: string;
    token_type: string;
}

const environment = getE2EEnvironment();
const hostname = new URL(environment.appUrl).hostname;
test.skip(
    environment.isProduction || (!['127.0.0.1', '[::1]', 'localhost'].includes(hostname) && !hostname.endsWith('.localhost')),
    'This synthetic OAuth journey requires the local development test host.'
);
test.use({ e2eUseGeneratedUser: true });

test('AuthorizeConsent_ExpandedClientRegistration_RequiresFreshMemberGrant', async ({ browser, e2eApi, e2eScenario, page, request }) => {
    const readScopes = ['mcp:read', 'projects:read', 'stacks:read', 'events:read'];
    const allScopes = [...readScopes, 'stacks:write', 'offline_access'];
    const applicationName = `OAuth live ${e2eScenario.run}`;
    const administratorToken = await e2eApi.login();
    const administratorHeaders = { Authorization: `Bearer ${administratorToken}` };
    const memberHeaders = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const administratorContext = await browser.newContext({ baseURL: environment.appUrl, ignoreHTTPSErrors: true });
    let clientId: string | undefined;
    let applicationId: string | undefined;
    const issuedTokens: string[] = [];

    async function findApplication() {
        const response = await request.get(`${environment.apiUrl}/admin/oauth-applications`, {
            headers: administratorHeaders,
            params: { criteria: clientId!, limit: 10 }
        });
        expect(response.status()).toBe(200);
        const applications = (await response.json()) as OAuthApplication[];
        return applications.find((application) => application.client_id === clientId);
    }

    try {
        // The existing organization page is the synthetic client's callback. Observe real
        // browser navigation there; no OAuth, administrator or callback responses are intercepted.
        const redirectUri = `${environment.appUrl}/organization/${e2eScenario.organizationId}/manage`;
        const metadataResponse = await request.get(`${environment.appUrl}/.well-known/oauth-protected-resource/mcp`);
        expect(metadataResponse.status()).toBe(200);
        const { resource } = (await metadataResponse.json()) as { resource: string };

        await test.step('register a restricted client and show its actual denied-scope response', async () => {
            // Act
            const registrationResponse = await request.post(`${environment.apiUrl}/oauth/register`, {
                data: {
                    client_name: applicationName,
                    grant_types: ['authorization_code', 'refresh_token'],
                    redirect_uris: [redirectUri],
                    response_types: ['code'],
                    scope: readScopes.join(' '),
                    token_endpoint_auth_method: 'none'
                }
            });
            // Assert
            expect(registrationResponse.status()).toBe(201);
            clientId = ((await registrationResponse.json()) as { client_id: string }).client_id;
            expect(clientId).toMatch(/^dcr_/);
            await expect.poll(async () => (applicationId = (await findApplication())?.id)).toBeTruthy();

            const forbidden = await request.put(`${environment.apiUrl}/admin/oauth-applications/${applicationId}`, {
                data: { client_id: clientId, is_disabled: false, name: applicationName, redirect_uris: [redirectUri], scopes: allScopes },
                headers: memberHeaders
            });
            expect(forbidden.status()).toBe(403);

            await page.goto(authorizationUrl(allScopes));
            await expect(page.getByText(/^Scopes not allowed for this application: stacks:write, offline_access\./)).toBeVisible();
            await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeDisabled();
            await expect(page.getByRole('checkbox', { name: /Stacks Write/ })).toBeDisabled();
            await expect(page.getByRole('checkbox', { name: /Offline Access/ })).toBeDisabled();
            await expect(page.getByRole('checkbox', { exact: true, name: e2eScenario.organizationName })).toBeDisabled();
        });

        await administratorContext.addInitScript((token) => window.localStorage.setItem('satellizer_token', token), administratorToken);
        const administratorPage = await administratorContext.newPage();
        await test.step('find the failed registration through Not authorized', async () => {
            // Act
            await administratorPage.goto(`/system/oauth-applications?criteria=${encodeURIComponent(applicationName)}`);
            // Assert
            await expect(administratorPage.getByRole('button', { name: 'Filter by authorization' })).toHaveText('Authorized');
            await expect(administratorPage.getByRole('link', { exact: true, name: applicationName })).toHaveCount(0);
            await administratorPage.getByRole('button', { name: 'Filter by authorization' }).click();
            await administratorPage.getByRole('option', { exact: true, name: 'Not authorized' }).click();
            await expect(administratorPage.getByRole('link', { exact: true, name: applicationName })).toBeVisible();
            await administratorPage.getByRole('button', { name: `Show details for ${applicationName}` }).click();
            await expect(administratorPage.getByText(clientId!, { exact: true })).toBeVisible();
            await administratorPage.getByRole('link', { exact: true, name: 'Edit application' }).click();
            await expect(administratorPage.getByLabel('Client ID', { exact: true })).toHaveValue(clientId!);
        });

        const limitedGrant = await test.step('consent to an allowed subset without offline access', async () => {
            // Act
            const tokens = await approveAndExchange(readScopes);
            // Assert
            expect(tokens.refresh_token).toBeUndefined();
            expect(tokens.scope.split(' ')).toEqual(readScopes);
            return tokens;
        });

        await test.step('save the reviewed scopes in the real administrator form without replacing the client', async () => {
            await expect(administratorPage.getByLabel('Client ID', { exact: true })).toHaveValue(clientId!);
            for (const label of ['Stacks Write', 'Offline Access']) {
                const checkbox = administratorPage.getByText(label, { exact: true }).locator('../..').getByRole('checkbox');
                await expect(checkbox).not.toBeChecked();
                await checkbox.check();
            }
            const savedResponse = administratorPage.waitForResponse(
                (response) => response.request().method() === 'PUT' && new URL(response.url()).pathname === `/api/v2/admin/oauth-applications/${applicationId}`
            );
            await administratorPage.getByRole('button', { exact: true, name: 'Save Changes' }).click();
            expect((await savedResponse).status()).toBe(200);
            await expect(administratorPage).toHaveURL((url) => url.pathname === '/system/oauth-applications');
            const savedApplication = await findApplication();
            expect(savedApplication?.client_id).toBe(clientId);
            expect(savedApplication?.scopes.toSorted()).toEqual(allScopes.toSorted());
            expect(savedApplication?.organizations.map((organization) => organization.id)).toEqual([e2eScenario.organizationId]);
            const oldGrantRefresh = await request.post(`${environment.apiUrl}/oauth/token`, {
                form: { client_id: clientId!, grant_type: 'refresh_token', refresh_token: limitedGrant.access_token }
            });
            expect(oldGrantRefresh.status()).toBe(400);
            expect((await oldGrantRefresh.json()).error).toBe('invalid_grant');
        });

        await test.step('restart with the same client and exchange and rotate the fresh consent grant', async () => {
            // Act
            const granted = await approveAndExchange(allScopes);
            // Assert
            expect(granted.scope.split(' ').toSorted()).toEqual(allScopes.toSorted());
            expect(granted.refresh_token).toEqual(expect.any(String));
            const response = await request.post(`${environment.apiUrl}/oauth/token`, {
                form: { client_id: clientId!, grant_type: 'refresh_token', refresh_token: granted.refresh_token! }
            });
            expect(response.status()).toBe(200);
            const refreshed = (await response.json()) as OAuthTokens;
            issuedTokens.push(refreshed.access_token);
            expect(refreshed.access_token).not.toBe(granted.access_token);
            expect(refreshed.refresh_token).toEqual(expect.any(String));
            expect(refreshed.refresh_token).not.toBe(granted.refresh_token);
            expect(refreshed.scope.split(' ').toSorted()).toEqual(allScopes.toSorted());
            const resourceResponse = await request.get(resource, { headers: { Authorization: `Bearer ${refreshed.access_token}` } });
            // The stateless MCP endpoint rejects GET only after authenticating the OAuth grant.
            expect(resourceResponse.status()).toBe(405);
            const application = await findApplication();
            expect(application?.organizations.map((organization) => organization.id)).toEqual([e2eScenario.organizationId]);
        });

        function authorizationUrl(scopes: string[], verifier = randomBytes(32).toString('base64url')) {
            return `/oauth/authorize?${new URLSearchParams({
                client_id: clientId!,
                code_challenge: createHash('sha256').update(verifier).digest('base64url'),
                code_challenge_method: 'S256',
                redirect_uri: redirectUri,
                resource,
                response_type: 'code',
                scope: scopes.join(' '),
                state: e2eScenario.run
            })}`;
        }

        async function approveAndExchange(scopes: string[]) {
            const verifier = randomBytes(32).toString('base64url');
            await page.goto(authorizationUrl(scopes, verifier));
            await expect(page.getByRole('checkbox', { exact: true, name: e2eScenario.organizationName })).toBeChecked();
            await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeEnabled();
            await expect(page.getByRole('checkbox', { name: /Projects Read/ })).toBeEnabled();
            await expect(page.getByRole('checkbox', { exact: true, name: e2eScenario.organizationName })).toBeEnabled();
            const authorizationResponse = page.waitForResponse(
                (response) => response.request().method() === 'POST' && new URL(response.url()).pathname === '/api/v2/oauth/authorize'
            );
            await page.getByRole('button', { exact: true, name: 'Approve' }).click();
            const response = await authorizationResponse;
            expect(response.status()).toBe(200);
            expect(response.request().postDataJSON()).toMatchObject({
                client_id: clientId,
                organization_ids: [e2eScenario.organizationId],
                scope: scopes.join(' ')
            });
            await expect(page).toHaveURL((url) => url.pathname === new URL(redirectUri).pathname && url.searchParams.has('code'));
            const callback = new URL(page.url());
            expect(callback.searchParams.get('state')).toBe(e2eScenario.run);
            const code = callback.searchParams.get('code');
            expect(code).toBeTruthy();
            const exchange = await request.post(`${environment.apiUrl}/oauth/token`, {
                form: {
                    client_id: clientId!,
                    code: code!,
                    code_verifier: verifier,
                    grant_type: 'authorization_code',
                    redirect_uri: redirectUri,
                    resource
                }
            });
            expect(exchange.status()).toBe(200);
            const tokens = (await exchange.json()) as OAuthTokens;
            expect(tokens.access_token).toEqual(expect.any(String));
            expect(tokens.token_type).toBe('Bearer');
            issuedTokens.push(tokens.access_token);
            return tokens;
        }
    } finally {
        const cleanupErrors: Error[] = [];
        await runCleanupStep(cleanupErrors, 'close administrator browser', () => administratorContext.close());
        for (const token of issuedTokens) {
            await runCleanupStep(cleanupErrors, 'revoke synthetic OAuth grant', async () => {
                const response = await request.post(`${environment.apiUrl}/oauth/revoke`, { form: { client_id: clientId!, token } });
                expect(response.status()).toBe(200);
            });
        }
        if (clientId) {
            await runCleanupStep(cleanupErrors, 'delete synthetic OAuth application', async () => {
                applicationId ??= (await findApplication())?.id;
                expect(applicationId).toBeTruthy();
                const response = await request.delete(`${environment.apiUrl}/admin/oauth-applications/${applicationId}`, { headers: administratorHeaders });
                expect([204, 404]).toContain(response.status());
            });
        }
        throwIfCleanupFailed(cleanupErrors);
    }
});
