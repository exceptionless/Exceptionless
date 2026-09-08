import type { Page } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';

test('organization deep links select the destination organization', async ({ e2eScenario, e2eSecondaryOrganization, page }) => {
    await page.goto(`/next/organization/${e2eSecondaryOrganization.organizationId}/manage?from=link#settings`);

    await expect(page).toHaveURL(new RegExp(`/organization/${e2eSecondaryOrganization.organizationId}/manage\\?from=link#settings$`));
    await expect(page.getByRole('heading', { exact: true, name: `${e2eSecondaryOrganization.organizationName} Settings` })).toBeVisible();
    await expectOrganization(page, e2eSecondaryOrganization.organizationId);
    expect(e2eScenario.organizationId).not.toBe(e2eSecondaryOrganization.organizationId);
});

for (const resource of ['project', 'stack', 'event', 'stack event', 'project stack'] as const) {
    test(`${resource} deep links select the destination organization`, async ({ e2eApi, e2eScenario, e2eSecondaryOrganization, page }) => {
        const event = resource === 'project' ? undefined : await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eSecondaryOrganization);
        const href =
            resource === 'project'
                ? `/next/project/${e2eSecondaryOrganization.projectId}/settings`
                : resource === 'project stack'
                  ? `/next/project/${e2eSecondaryOrganization.projectId}/stacks/${event!.stack_id}`
                  : resource === 'stack'
                    ? `/next/stack/${event!.stack_id}`
                    : resource === 'event'
                      ? `/next/event/${event!.id}`
                      : `/next/stack/${event!.stack_id}/event/${event!.id}`;

        await page.goto(href);
        await expectOrganization(page, e2eSecondaryOrganization.organizationId);
        if (event) {
            await expect(page.getByText(e2eSecondaryOrganization.message, { exact: true }).filter({ visible: true }).first()).toBeVisible();
            await expect(page).toHaveURL(resource === 'project stack' ? href : new RegExp(`/stack/${event.stack_id}/event/${event.id}$`));
        } else {
            await expect(page.getByRole('heading', { exact: true, name: `${e2eSecondaryOrganization.projectName} Settings` })).toBeVisible();
        }

        await page.reload();
        await expectOrganization(page, e2eSecondaryOrganization.organizationId);
        await page.getByRole('button').filter({ hasText: e2eSecondaryOrganization.organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem').filter({ hasText: e2eScenario.organizationName }).click();
        await expectOrganization(page, e2eScenario.organizationId);
        await expect(page).toHaveURL(/\/next\/stack\/all$/);
    });
}

async function expectOrganization(page: Page, organizationId: string): Promise<void> {
    await expect(async () => {
        const activeOrganizationId = await page.evaluate(() => JSON.parse(localStorage.getItem('organization') ?? 'null'));
        expect(activeOrganizationId).toBe(organizationId);
    }).toPass({ timeout: 10_000 });
}

test('Exie usage organization links switch context and support browser history', async ({ e2eScenario, e2eSecondaryOrganization, page }) => {
    const organizations = [e2eScenario, e2eSecondaryOrganization];
    await page.route('**/api/v2/admin/assistant-usage?*', async (route) => {
        await route.fulfill({
            json: {
                active_organizations: 2,
                completion_tokens: 0,
                cost_usd: 0,
                month: '2026-09-01',
                organizations: organizations.map((item) => ({
                    blocked_by_concurrency: 0,
                    blocked_by_cost_limit: 0,
                    blocked_by_rate_limit: 0,
                    blocked_by_token_limit: 0,
                    cancelled: 0,
                    completed: 0,
                    completion_tokens: 0,
                    cost_usd: 0,
                    failed: 0,
                    last_used_utc: new Date().toISOString(),
                    organization_id: item.organizationId,
                    organization_name: item.organizationName,
                    plan_id: 'FREE',
                    prompt_tokens: 0,
                    provider_requests: 0,
                    tool_calls: 0,
                    turns: 0
                })),
                prompt_tokens: 0,
                turns: 0
            }
        });
    });

    await page.goto('/next/system/exie');
    await page.getByRole('link', { exact: true, name: e2eSecondaryOrganization.organizationName }).click();
    await expect(page.getByRole('heading', { exact: true, name: `${e2eSecondaryOrganization.organizationName} Settings` })).toBeVisible();
    await expectOrganization(page, e2eSecondaryOrganization.organizationId);
    await expect(page).toHaveURL(new RegExp(`/organization/${e2eSecondaryOrganization.organizationId}/manage$`));

    await test.info().attach('organization-link-destination', { body: await page.screenshot(), contentType: 'image/png' });
    await page.goto(`/next/organization/${e2eScenario.organizationId}/manage`);
    await expectOrganization(page, e2eScenario.organizationId);
    await expect(page.getByRole('heading', { exact: true, name: `${e2eScenario.organizationName} Settings` })).toBeVisible();
    await page.goBack();
    await expect(page).toHaveURL(new RegExp(`/organization/${e2eSecondaryOrganization.organizationId}/manage$`));
    await expectOrganization(page, e2eSecondaryOrganization.organizationId);
    await page.goForward();
    await expect(page).toHaveURL(new RegExp(`/organization/${e2eScenario.organizationId}/manage$`));
    await expectOrganization(page, e2eScenario.organizationId);
});

test('organization users and billing links request the destination organization', async ({ e2eScenario, e2eSecondaryOrganization, page }) => {
    const usersResponse = page.waitForResponse((response) => response.url().includes(`/organizations/${e2eSecondaryOrganization.organizationId}/users`));
    await page.goto(`/next/organization/${e2eSecondaryOrganization.organizationId}/users`);
    expect((await usersResponse).ok()).toBe(true);
    await expectOrganization(page, e2eSecondaryOrganization.organizationId);
    await expect(page.getByRole('button', { name: 'Invite User' })).toBeVisible();

    const billingResponse = page.waitForResponse((response) => response.url().includes(`/organizations/${e2eScenario.organizationId}/invoices`));
    await page.goto(`/next/organization/${e2eScenario.organizationId}/billing`);
    expect([200, 404]).toContain((await billingResponse).status());
    await expectOrganization(page, e2eScenario.organizationId);
    await expect(page).toHaveURL(new RegExp(`/organization/${e2eScenario.organizationId}/billing$`));
});

test('organization project links switch before redirecting to the project list', async ({ e2eSecondaryOrganization, page }) => {
    await page.goto(`/next/organization/${e2eSecondaryOrganization.organizationId}/projects`);
    await expectOrganization(page, e2eSecondaryOrganization.organizationId);
    await expect(page).toHaveURL(/\/next\/project\/list/);
    await expect(page.getByText(e2eSecondaryOrganization.projectName, { exact: true }).filter({ visible: true }).first()).toBeVisible();
});

test('stack links select the owner when no events remain', async ({ e2eApi, e2eScenario, e2eSecondaryOrganization, page }) => {
    const event = await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eSecondaryOrganization);
    await page.route(`**/api/v2/stacks/${event.stack_id}/events?*`, async (route) => {
        await route.fulfill({ json: [] });
    });
    await page.goto(`/next/stack/${event.stack_id}`);
    await expect(page.getByText('No events available for this stack.')).toBeVisible();
    await expectOrganization(page, e2eSecondaryOrganization.organizationId);
    await expect(page).toHaveURL(new RegExp(`/stack/${event.stack_id}$`));
});

test('authorized invoice links select their organization', async ({ e2eSecondaryOrganization, page }) => {
    const invoiceId = 'navigation-invoice';
    await page.route(`**/api/v2/organizations/invoice/${invoiceId}`, async (route) => {
        await route.fulfill({
            json: {
                date: new Date().toISOString(),
                id: invoiceId,
                items: [],
                organization_id: e2eSecondaryOrganization.organizationId,
                organization_name: e2eSecondaryOrganization.organizationName,
                paid: true,
                status: 'paid',
                total: 0
            }
        });
    });
    await page.goto(`/next/payment/${invoiceId}`);
    await expect(page.getByRole('cell', { exact: true, name: e2eSecondaryOrganization.organizationName })).toBeVisible();
    await expectOrganization(page, e2eSecondaryOrganization.organizationId);
    await expect(page).toHaveURL(new RegExp(`/payment/${invoiceId}$`));
});

test.describe('member access', () => {
    test.use({ e2eUseGeneratedUser: true });

    test('members can follow links to another organization they belong to', async ({ e2eScenario, e2eSecondaryOrganization, page }) => {
        await page.goto(`/next/organization/${e2eSecondaryOrganization.organizationId}/manage`);
        await expect(page.getByRole('heading', { exact: true, name: `${e2eSecondaryOrganization.organizationName} Settings` })).toBeVisible();
        await expectOrganization(page, e2eSecondaryOrganization.organizationId);
        await page.goto(`/next/project/${e2eScenario.projectId}/settings`);
        await expect(page.getByRole('heading', { exact: true, name: `${e2eScenario.projectName} Settings` })).toBeVisible();
        await expectOrganization(page, e2eScenario.organizationId);
    });

    for (const status of [403, 404]) {
        test(`denied resource lookups (${status}) preserve the current organization`, async ({ e2eScenario, page }) => {
            const resourceId = '000000000000000000000001';
            await page.route(new RegExp(`/api/v2/(organizations|projects|stacks|events)/${resourceId}(?:[/?]|$)`), async (route) => {
                await route.fulfill({ body: JSON.stringify({ status, title: 'Resource unavailable' }), contentType: 'application/problem+json', status });
            });
            for (const resource of ['organization', 'project', 'stack', 'event']) {
                const response = page.waitForResponse((item) => item.url().includes(`/api/v2/${resource}s/${resourceId}`));
                const suffix = resource === 'organization' ? '/manage' : resource === 'project' ? '/settings' : '';
                await page.goto(`/next/${resource}/${resourceId}${suffix}`);
                expect((await response).status()).toBe(status);
                await expect(
                    page
                        .getByText(/Resource unavailable|could not be found|Unable to load stack event details/)
                        .filter({ visible: true })
                        .first()
                ).toBeVisible();
                await expectOrganization(page, e2eScenario.organizationId);
            }
        });
    }
});
