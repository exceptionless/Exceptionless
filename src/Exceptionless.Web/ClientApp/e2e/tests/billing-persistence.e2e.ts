import { expect, test } from '../fixtures/e2e-test';

test.use({ e2eUseGeneratedUser: true });

test('billing information persists across reloads and clearing removes its stored key', async ({ e2eScenario, page, request }) => {
    const headers = { Authorization: `Bearer ${e2eScenario.userToken}` };
    const organizationUrl = `/api/v2/organizations/${e2eScenario.organizationId}`;
    const values = {
        billing_address: '123 Example Street',
        billing_name: 'Billing persistence test',
        billing_vat_id: 'VAT-ID-123',
        billing_vat_number: 'VAT-456'
    };
    const readData = async () => {
        const response = await request.get(organizationUrl, { headers });
        expect(response.ok()).toBe(true);
        return (await response.json()).data as Record<string, unknown>;
    };

    await page.goto(`/organization/${e2eScenario.organizationId}/billing`);
    await page.getByRole('textbox', { exact: true, name: 'Billing name' }).fill(values.billing_name);
    await page.getByRole('textbox', { exact: true, name: 'Billing address' }).fill(values.billing_address);
    await page.getByRole('textbox', { exact: true, name: 'VAT ID' }).fill(values.billing_vat_id);
    await page.getByRole('textbox', { exact: true, name: 'VAT number' }).fill(values.billing_vat_number);
    await page.getByRole('textbox', { exact: true, name: 'VAT number' }).blur();
    await expect.poll(readData).toMatchObject(values);
    await page.reload();
    await expect(page.getByRole('textbox', { exact: true, name: 'Billing name' })).toHaveValue(values.billing_name);
    await expect(page.getByRole('textbox', { exact: true, name: 'Billing address' })).toHaveValue(values.billing_address);
    await expect(page.getByRole('textbox', { exact: true, name: 'VAT ID' })).toHaveValue(values.billing_vat_id);
    await expect(page.getByRole('textbox', { exact: true, name: 'VAT number' })).toHaveValue(values.billing_vat_number);

    await page.getByRole('textbox', { exact: true, name: 'VAT number' }).fill('');
    await page.getByRole('textbox', { exact: true, name: 'VAT number' }).blur();
    await expect.poll(readData).not.toHaveProperty('billing_vat_number');
    await expect
        .poll(readData)
        .toMatchObject({ billing_address: values.billing_address, billing_name: values.billing_name, billing_vat_id: values.billing_vat_id });
    await page.reload();
    await expect(page.getByRole('textbox', { exact: true, name: 'VAT number' })).toHaveValue('');
});
