import { expect, test } from '../fixtures/organization-test';

for (const iconState of ['missing', 'broken']) {
    test(`organization avatars handle an impersonated organization with a ${iconState} icon without a reload`, async ({
        e2eScenario,
        impersonatedOrganization,
        page
    }, testInfo) => {
        if (iconState === 'broken') {
            await page.route(`**/api/v2/organizations/${impersonatedOrganization.organizationId}`, async (route) => {
                const response = await route.fetch();
                const organization = await response.json();
                await route.fulfill({ json: { ...organization, icon_url: '/next/e2e-unavailable-organization-icon.png' }, response });
            });
            await page.route('**/e2e-unavailable-organization-icon.png', async (route) => {
                await route.fulfill({ body: '', status: 404 });
            });
        }
        await page.goto(`/next/organization/${e2eScenario.organizationId}/manage`);
        await expect(page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first()).toBeVisible();
        await page.getByLabel('Upload organization icon').setInputFiles('static/favicon.png');

        const organizationAvatar = page.getByTitle('Organization Avatar', { exact: true }).filter({ visible: true });
        await expect(organizationAvatar.locator('img')).toBeVisible();
        await expect.poll(() => organizationAvatar.locator('img').evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);

        await page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { name: 'Impersonate Organization' }).click();
        const dialog = page.getByRole('dialog', { name: 'Impersonate Organization' });
        await dialog.getByPlaceholder('Search by name or Id...').fill(impersonatedOrganization.organizationId);
        await dialog.getByRole('button').filter({ hasText: impersonatedOrganization.organizationName }).click();
        await dialog.getByRole('button', { exact: true, name: 'Impersonate' }).click();
        await expect(dialog).toBeHidden();
        await expect(
            page
                .getByRole('button')
                .filter({ hasText: impersonatedOrganization.organizationName })
                .filter({ hasText: 'Impersonating' })
                .filter({ visible: true })
        ).toBeVisible();
        if (iconState === 'broken') {
            await expect(organizationAvatar).toHaveAttribute('data-status', 'error');
        }
        await expect(organizationAvatar.getByText('IO', { exact: true })).toBeVisible();
        await expect(organizationAvatar.locator('img')).toBeHidden();
        await page.screenshot({ path: testInfo.outputPath('impersonated-organization-avatar.png') });

        await page.getByRole('button').filter({ hasText: impersonatedOrganization.organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { name: 'Stop Impersonating' }).click();
        // Stop Impersonating selects the first membership, which need not be this test's organization.
        await page.getByTitle('Organization Avatar', { exact: true }).filter({ visible: true }).click();
        await page.getByRole('menuitem').filter({ hasText: e2eScenario.organizationName }).click();
        await expect(organizationAvatar.locator('img')).toBeVisible();
        await expect.poll(() => organizationAvatar.locator('img').evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    });
}

test.describe('single organization avatars', () => {
    test.use({ e2eUseGeneratedUser: true });

    test('removing a loaded icon restores initials in the sidebar and settings', async ({ e2eScenario, page }) => {
        await page.goto(`/next/organization/${e2eScenario.organizationId}/manage`);
        await expect(page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first()).toBeVisible();
        await page.getByLabel('Upload organization icon').setInputFiles('static/favicon.png');

        const organizationAvatars = page.getByTitle('Organization Icon', { exact: true }).filter({ visible: true });
        await expect(organizationAvatars).toHaveCount(2);
        for (const avatar of await organizationAvatars.all()) {
            await expect(avatar.locator('img')).toBeVisible();
            await expect.poll(() => avatar.locator('img').evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
        }

        await page.getByRole('button', { name: 'Remove custom organization icon' }).click();
        await expect(page.getByRole('button', { name: 'Remove custom organization icon' })).toBeHidden();
        for (const avatar of await organizationAvatars.all()) {
            await expect(avatar.getByText('EP', { exact: true })).toBeVisible();
        }
    });
});
