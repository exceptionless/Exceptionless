import { expect, test } from '../fixtures/e2e-test';

test('global admin can open the guarded migration rerun confirmation', async ({ e2eScenario, page }) => {
    await page.route('**/api/v2/admin/migrations', async (route) => {
        await route.fulfill({
            body: JSON.stringify({
                current_version: 9,
                states: [
                    {
                        can_rerun: true,
                        completed_utc: '2026-09-04T12:00:00Z',
                        id: '5',
                        migration_type: 2,
                        started_utc: '2026-09-04T11:59:00Z',
                        version: 5
                    }
                ]
            }),
            contentType: 'application/json',
            status: 200
        });
    });

    await page.goto('/next/system/migrations');

    await expect(page.getByRole('heading', { name: 'System Administration' })).toBeVisible();
    await expect(page.getByText(e2eScenario.organizationName, { exact: false }).first()).toBeVisible();

    await page.getByRole('button', { name: 'Open actions for migration 5' }).click();
    await page.getByRole('menuitem', { name: 'Rerun migration' }).click();

    const dialog = page.getByRole('alertdialog', { name: 'Rerun Migration 5' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('does not change the current migration version')).toBeVisible();

    const rerunButton = dialog.getByRole('button', { name: 'Rerun Migration' });
    await expect(rerunButton).toBeDisabled();
    await dialog.getByLabel('Enter RERUN 5 to confirm').fill('RERUN 5');
    await expect(rerunButton).toBeEnabled();
});
