import { expect, test } from '../fixtures/e2e-test';

test('authenticated application pages defer loading Stripe until billing is opened', async ({ e2eApi, page }) => {
    const userToken = await e2eApi.login();
    const organizations = await e2eApi.getOrganizations(userToken);
    const organizationId = organizations[0]?.id;
    expect(organizationId).toBeTruthy();

    await page.addInitScript(
        ({ organizationId, token }) => {
            window.localStorage.setItem('satellizer_token', token);
            window.localStorage.setItem('organization', JSON.stringify(organizationId));
        },
        { organizationId, token: userToken }
    );

    const stripeRequests: string[] = [];
    page.on('request', (request) => {
        const hostname = new URL(request.url()).hostname;
        if (hostname === 'js.stripe.com' || hostname === 'm.stripe.com' || hostname.endsWith('.stripe.network')) {
            stripeRequests.push(request.url());
        }
    });

    await page.goto('/next/stack/all');
    await expect(page.getByRole('heading', { exact: true, name: 'All' })).toBeVisible();
    await expect(page.getByTitle('Refresh results').locator('svg')).not.toHaveClass(/animate-spin/);
    await page.waitForTimeout(1_000);

    expect(stripeRequests).toEqual([]);
});
