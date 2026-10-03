import { expect, test } from '@playwright/test';

test('OAuth consent displays scope-policy errors and validates a restarted request before approval', async ({ page }) => {
    const errorDescription =
        'Scopes not allowed for this application: stacks:write, offline_access. Restart authorization with fewer scopes or ask a global administrator to review the application in System → OAuth Apps. After saving changes, restart authorization using the same client ID.';
    let consentRequests = 0;
    let authorizationRequests = 0;
    let completeStaleConsent: (() => void) | undefined;
    await page.addInitScript(() => window.localStorage.setItem('satellizer_token', 'test-consent-session'));
    // This browser regression exercises the production page and FetchClient with HTTP responses.
    // Service policy and issuance are covered separately by OAuthEndpointTests.
    await page.route('**/api/v2/**', async (route) => {
        const path = new URL(route.request().url()).pathname;
        if (path.endsWith('/users/me')) {
            await route.fulfill({ json: { email_address: 'member@example.test', full_name: 'Member' } });
        } else if (path.endsWith('/organizations')) {
            await route.fulfill({ json: [{ id: '000000000000000000000001', name: 'Test Organization' }] });
        } else if (path.endsWith('/oauth/authorize/consent')) {
            consentRequests++;
            const body = route.request().postDataJSON() as { client_id: string; scope: string };
            if (body.client_id === 'stale-client') {
                await new Promise<void>((resolve) => {
                    completeStaleConsent = resolve;
                });
                await route.fulfill({ json: { error: 'invalid_scope', error_description: 'Stale request error' }, status: 400 });
                return;
            }
            if (body.scope.includes('stacks:write')) {
                await route.fulfill({ json: { error: 'invalid_scope', error_description: errorDescription }, status: 400 });
            } else {
                await route.fulfill({ json: { client_name: 'Test Client', required_scopes: ['mcp:read'], scopes: body.scope.split(' ') } });
            }
        } else if (path.endsWith('/oauth/authorize')) {
            authorizationRequests++;
            await route.fulfill({ json: { error: 'invalid_scope', error_description: errorDescription }, status: 400 });
        } else {
            await route.abort();
        }
    });

    const parameters = new URLSearchParams({
        client_id: 'test-client',
        code_challenge: 'a'.repeat(43),
        code_challenge_method: 'S256',
        redirect_uri: 'http://localhost/callback',
        resource: 'http://localhost/mcp',
        response_type: 'code',
        scope: 'mcp:read projects:read stacks:read stacks:write events:read offline_access'
    });
    try {
        await page.goto(`/oauth/authorize?${parameters}`);
        await expect(page.getByText(errorDescription, { exact: true })).toBeVisible();
        await expect(page.getByText('Required', { exact: true })).toHaveCount(1);
        await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeDisabled();
        await page.getByRole('checkbox', { name: /Stacks Write/ }).click();
        await page.getByRole('checkbox', { name: /Offline Access/ }).click();
        await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeDisabled();
        expect(consentRequests).toBe(1);
        expect(authorizationRequests).toBe(0);
        await page.screenshot({ fullPage: true, path: test.info().outputPath('scope-error.png') });

        parameters.set('scope', 'mcp:read');
        await page.goto(`/oauth/authorize?${parameters}`);
        await expect(page.getByText('Test Client', { exact: true })).toBeVisible();
        await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeEnabled();
        expect(consentRequests).toBe(2);
        await page.getByRole('button', { exact: true, name: 'Approve' }).click();
        await expect(page.getByText(errorDescription, { exact: true })).toBeVisible();
        expect(authorizationRequests).toBe(1);

        async function navigateWithQuery() {
            await page.evaluate((href) => {
                document.getElementById('oauth-query-link')?.remove();
                const link = document.createElement('a');
                link.id = 'oauth-query-link';
                link.href = href;
                link.textContent = 'Change authorization request';
                document.body.append(link);
            }, `/oauth/authorize?${parameters}`);
            await page.getByRole('link', { name: 'Change authorization request' }).click();
        }

        parameters.set('scope', 'mcp:read projects:read');
        await navigateWithQuery();
        await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeEnabled();
        await expect(page.getByText(errorDescription, { exact: true })).toHaveCount(0);

        parameters.set('client_id', 'stale-client');
        await navigateWithQuery();
        await expect.poll(() => Boolean(completeStaleConsent)).toBe(true);
        await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeDisabled();
        parameters.set('client_id', 'test-client');
        await navigateWithQuery();
        await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeEnabled();
        const staleResponse = page.waitForResponse(
            (response) => response.url().endsWith('/oauth/authorize/consent') && response.request().postDataJSON().client_id === 'stale-client'
        );
        completeStaleConsent?.();
        await (await staleResponse).finished();
        await expect(page.getByText('Stale request error', { exact: true })).toHaveCount(0);
        await expect(page.getByRole('button', { exact: true, name: 'Approve' })).toBeEnabled();
    } finally {
        completeStaleConsent?.();
    }
});
