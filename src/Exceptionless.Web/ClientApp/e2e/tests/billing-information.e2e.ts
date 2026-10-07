import { expect, test } from '@playwright/test';

const ORGANIZATION_ID = '000000000000000000000001';
const USER_ID = '000000000000000000000002';

for (const width of [1440, 390]) {
    test(`billing information autosaves and clears at ${width}px`, async ({ page }, testInfo) => {
        await page.setViewportSize({ height: 1000, width });
        const data: Record<string, string> = { unrelated_key: 'preserved' };
        const organization = { data, features: [], id: ORGANIZATION_ID, name: 'Billing Test', plan_id: 'EX_FREE', plan_name: 'Free' };
        const writes: string[] = [];
        let rejectNextWrite = false;
        const writeGate = { pending: undefined as Promise<void> | undefined };
        await page.addInitScript((organizationId) => {
            localStorage.setItem('satellizer_token', 'billing-test-token');
            localStorage.setItem('organization', JSON.stringify(organizationId));
        }, ORGANIZATION_ID);
        await page.route('**/health', (route) => route.fulfill({ body: 'OK' }));
        await page.route('**/api/v2/**', async (route) => {
            const request = route.request();
            const path = new URL(request.url()).pathname;
            const dataPrefix = `/api/v2/organizations/${ORGANIZATION_ID}/data/`;
            if (path.startsWith(dataPrefix)) {
                await writeGate.pending;
                if (rejectNextWrite) {
                    rejectNextWrite = false;
                    await route.fulfill({
                        contentType: 'application/problem+json',
                        json: { detail: 'Please retry saving.', status: 503, title: 'Save unavailable' },
                        status: 503
                    });
                    return;
                }
                const key = decodeURIComponent(path.slice(dataPrefix.length));
                writes.push(`${request.method()}:${key}`);
                if (request.method() === 'DELETE') {
                    delete data[key];
                } else {
                    data[key] = (request.postDataJSON() as { value: string }).value;
                }
                await route.fulfill({ status: 200 });
                return;
            }
            if (path === '/api/v2/users/me') {
                await route.fulfill({
                    json: {
                        email_address: 'billing@example.test',
                        email_notifications_enabled: true,
                        full_name: 'Billing Tester',
                        has_local_account: true,
                        id: USER_ID,
                        is_active: true,
                        is_email_address_verified: true,
                        is_invite: false,
                        o_auth_accounts: [],
                        organization_ids: [ORGANIZATION_ID],
                        organization_preferences: [],
                        roles: []
                    }
                });
            } else if (path === '/api/v2/organizations') {
                await route.fulfill({ json: [organization] });
            } else if (path === `/api/v2/organizations/${ORGANIZATION_ID}`) {
                await route.fulfill({ json: organization });
            } else if (path === `/api/v2/organizations/${ORGANIZATION_ID}/invoices`) {
                await route.fulfill({ json: [{ date: '2026-07-01T12:00:00Z', id: 'invoice-1', paid: true, status: 'paid', total: 199 }] });
            } else if (path === '/api/v2/assistant/access') {
                await route.fulfill({ json: { enabled: false, has_access: false, message: null, upgrade_required: false } });
            } else {
                await route.fulfill({ json: [] });
            }
        });

        await page.goto(`/next/organization/${ORGANIZATION_ID}/billing`);
        const name = page.getByRole('textbox', { exact: true, name: 'Billing name' });
        await expect(name).toHaveValue('');
        await name.fill('  Acme, Inc.  ');
        await page.getByRole('textbox', { exact: true, name: 'Billing address' }).fill('123 Main Street\nAnytown');
        await page.getByRole('textbox', { exact: true, name: 'VAT ID' }).fill('DE123456789');
        await page.getByRole('textbox', { exact: true, name: 'VAT number' }).fill('123456789');
        await expect.poll(() => writes.length).toBe(4);
        expect(data).toEqual({
            billing_address: '123 Main Street\nAnytown',
            billing_name: 'Acme, Inc.',
            billing_vat_id: 'DE123456789',
            billing_vat_number: '123456789',
            unrelated_key: 'preserved'
        });
        await page.reload();
        await expect(name).toHaveValue('Acme, Inc.');
        await expect(page.getByRole('columnheader', { exact: true, name: 'Amount' })).toBeVisible();
        await expect(page.getByRole('cell', { exact: true, name: 'Paid' })).toBeVisible();
        await testInfo.attach(`billing-${width}`, { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });

        rejectNextWrite = true;
        await name.fill('Retry name');
        await expect(page.getByText(/Error saving billing information/).first()).toBeVisible();
        await expect(name).toHaveValue('Retry name');
        expect(data.billing_name).toBe('Acme, Inc.');
        await name.fill('Retried name');
        await expect.poll(() => data.billing_name).toBe('Retried name');
        await name.fill('   ');
        await expect.poll(() => data.billing_name).toBeUndefined();
        expect(writes).toContain('DELETE:billing_name');
        expect(data.unrelated_key).toBe('preserved');
        await page.reload();
        await expect(name).toHaveValue('');

        await name.fill('Saved before leaving');
        await page.getByRole('link', { exact: true, name: 'General' }).click();
        await expect(page).toHaveURL(new RegExp(`/organization/${ORGANIZATION_ID}/billing$`));
        await expect.poll(() => data.billing_name).toBe('Saved before leaving');

        const releaseWrite = Promise.withResolvers<void>();
        writeGate.pending = releaseWrite.promise;
        const pendingRequest = page.waitForRequest((request) => request.method() === 'POST' && request.url().endsWith('/data/billing_name'));
        await name.fill('Save in flight');
        await pendingRequest;
        await page.getByRole('link', { exact: true, name: 'General' }).click();
        await expect(page.getByText('Please wait for billing information to finish saving, then try navigating again.')).toBeVisible();
        await expect(page).toHaveURL(new RegExp(`/organization/${ORGANIZATION_ID}/billing$`));
        releaseWrite.resolve();
        await expect.poll(() => data.billing_name).toBe('Save in flight');
        await expect(page.getByText('Successfully updated billing information.').last()).toBeVisible();
        await page.getByRole('link', { exact: true, name: 'General' }).click();
        await expect(page).toHaveURL(new RegExp(`/organization/${ORGANIZATION_ID}/manage$`));
    });
}
