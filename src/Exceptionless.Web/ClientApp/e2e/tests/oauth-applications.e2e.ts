import type { OAuthApplication } from '../../src/lib/features/admin/models';

import { expect, test } from '../fixtures/e2e-test';

test('OAuth applications default to authorized and expose configuration and organization links', async ({ e2eScenario, page }) => {
    await page.setViewportSize({ height: 1100, width: 1440 });
    const authorizedApplication: OAuthApplication = {
        client_id: 'https://client.example/oauth/client-metadata',
        created_by_user_id: '000000000000000000000001',
        created_utc: '2026-09-01T12:00:00Z',
        id: '000000000000000000000101',
        is_disabled: false,
        name: 'Recently authorized application',
        notes: 'OAuth configuration for browser verification.',
        organizations: [
            { id: e2eScenario.organizationId, name: e2eScenario.organizationName },
            { id: '000000000000000000000103', name: 'Second authorized organization' }
        ],
        redirect_uris: ['https://client.example/oauth/callback', 'http://localhost:54321/callback'],
        scopes: ['mcp:read', 'events:read'],
        updated_utc: '2026-09-12T12:00:00Z'
    };
    const unauthorizedApplication: OAuthApplication = {
        ...authorizedApplication,
        client_id: 'dcr_pending-application',
        id: '000000000000000000000102',
        name: 'Pending application',
        organizations: [],
        updated_utc: '2026-09-11T12:00:00Z'
    };

    await page.route('**/api/v2/admin/oauth-applications?*', async (route) => {
        const params = new URL(route.request().url()).searchParams;
        const applications =
            params.get('authorized') === 'true'
                ? [authorizedApplication]
                : params.get('authorized') === 'false'
                  ? [unauthorizedApplication]
                  : [authorizedApplication, unauthorizedApplication];
        await route.fulfill({ json: applications });
    });

    const initialRequest = page.waitForRequest((request) => request.url().includes('/api/v2/admin/oauth-applications?'));
    await page.goto('/next/system/oauth-applications');
    const params = new URL((await initialRequest).url()).searchParams;
    expect(params.get('authorized')).toBe('true');
    expect(params.get('sort')).toBe('-updated_utc');
    await expect(page.getByRole('button', { name: 'Filter by authorization' })).toHaveText('Authorized');
    await expect(page.getByRole('link', { exact: true, name: authorizedApplication.name })).toBeVisible();
    await expect(page.getByRole('link', { exact: true, name: unauthorizedApplication.name })).toHaveCount(0);
    await expect(page.getByRole('columnheader', { name: 'Client ID' })).toHaveCount(0);
    await expect(page.getByText(authorizedApplication.client_id, { exact: true })).toHaveCount(0);

    await page.getByRole('button', { name: `Show details for ${authorizedApplication.name}` }).click();
    await expect(page.getByText(authorizedApplication.client_id, { exact: true })).toBeVisible();
    for (const uri of authorizedApplication.redirect_uris) {
        await expect(page.getByText(uri, { exact: true })).toBeVisible();
    }
    await expect(page.getByRole('button', { name: /copy/i })).toHaveCount(0);
    await expect(page.getByRole('link', { exact: true, name: 'Edit application' })).toHaveAttribute(
        'href',
        `/next/system/oauth-applications/${authorizedApplication.id}`
    );
    await expect(page.getByRole('link', { exact: true, name: 'Second authorized organization' })).toHaveAttribute(
        'href',
        '/next/organization/000000000000000000000103/manage'
    );

    await page.screenshot({ fullPage: true, path: test.info().outputPath('oauth-application-details.png') });

    await page.getByRole('button', { name: `Hide details for ${authorizedApplication.name}` }).click();
    await expect(page.getByText(authorizedApplication.client_id, { exact: true })).toHaveCount(0);
    await page.getByRole('button', { name: 'Filter by authorization' }).click();
    await page.getByRole('option', { exact: true, name: 'Not authorized' }).click();
    await expect(page.getByRole('link', { exact: true, name: unauthorizedApplication.name })).toBeVisible();
    await expect(page.getByRole('link', { exact: true, name: authorizedApplication.name })).toHaveCount(0);
    await expect(page).toHaveURL(/authorization=unauthorized/);

    await page.getByRole('button', { name: 'Filter by authorization' }).click();
    await page.getByRole('option', { exact: true, name: 'All applications' }).click();
    await expect(page.getByRole('link', { exact: true, name: authorizedApplication.name })).toBeVisible();
    await expect(page.getByRole('link', { exact: true, name: unauthorizedApplication.name })).toBeVisible();
    await expect(page).toHaveURL(/authorization=all/);
    await page.reload();
    await expect(page.getByRole('button', { name: 'Filter by authorization' })).toHaveText('All applications');

    await page.getByRole('link', { exact: true, name: e2eScenario.organizationName }).click();
    await expect(page).toHaveURL(new RegExp(`/next/organization/${e2eScenario.organizationId}/manage`));
});
