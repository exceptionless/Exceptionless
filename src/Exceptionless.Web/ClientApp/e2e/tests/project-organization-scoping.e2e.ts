import type { Page } from '@playwright/test';

import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';
import { runCleanupStep, throwIfCleanupFailed } from '../support/cleanup';
import { getVisibleText } from '../support/page-helpers';

test.describe('member project lists', () => {
    test.use({ e2eUseGeneratedUser: true });

    test('Projects follows the selected organization through switching and reload', async ({ e2eScenario, e2eSecondaryOrganization, page }) => {
        await page.goto('/next/project/list');
        await expect(getVisibleText(page, e2eScenario.projectName)).toBeVisible();
        await expect(getVisibleText(page, e2eSecondaryOrganization.projectName)).toBeHidden();

        await page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem').filter({ hasText: e2eSecondaryOrganization.organizationName }).click();
        await openProjects(page);

        await expect(getVisibleText(page, e2eSecondaryOrganization.projectName)).toBeVisible();
        await expect(getVisibleText(page, e2eScenario.projectName)).toBeHidden();

        await page.reload();
        await expect(getVisibleText(page, e2eSecondaryOrganization.projectName)).toBeVisible();
        await expect(getVisibleText(page, e2eScenario.projectName)).toBeHidden();

        await page.getByRole('button').filter({ hasText: e2eSecondaryOrganization.organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem').filter({ hasText: e2eScenario.organizationName }).click();
        await openProjects(page);

        await expect(getVisibleText(page, e2eScenario.projectName)).toBeVisible();
        await expect(getVisibleText(page, e2eSecondaryOrganization.projectName)).toBeHidden();
    });
});

test('Projects shows the impersonated organization instead of the administrator memberships', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    const email = `impersonation-${e2eScenario.run}@exceptionless.test`.toLowerCase();
    const organizationName = `Impersonated Organization ${e2eScenario.run}`;
    const projectName = `Impersonated Project ${e2eScenario.run}`;
    let ownerToken: string | undefined;
    let organizationId: string | undefined;
    let projectId: string | undefined;

    try {
        ownerToken = await e2eApi.createInvitedUser(
            e2eScenario.userToken,
            e2eScenario.organizationId,
            'Impersonated organization owner',
            email,
            E2E_TEST_PASSWORD
        );
        await e2eApi.deleteOrganizationUser(e2eScenario.userToken, e2eScenario.organizationId, email);
        await e2eApi.waitForOrganizationNotListed(ownerToken, e2eScenario.organizationId);
        const organization = await e2eApi.createOrganization(ownerToken, organizationName);
        organizationId = organization.id;
        await e2eApi.waitForOrganizationListed(ownerToken, organization.id);
        const project = await e2eApi.createProject(ownerToken, organization.id, projectName);
        projectId = project.id;

        await page.goto('/next/project/list');
        await expect(getVisibleText(page, e2eScenario.projectName)).toBeVisible();
        await page.getByRole('button').filter({ hasText: e2eScenario.organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { name: 'Impersonate Organization' }).click();

        const dialog = page.getByRole('dialog', { name: 'Impersonate Organization' });
        await dialog.getByPlaceholder('Search by name or Id...').fill(organization.id);
        await dialog.getByRole('button').filter({ hasText: organizationName }).click();
        await dialog.getByRole('button', { exact: true, name: 'Impersonate' }).click();
        await expect(dialog).toBeHidden();
        await expect(
            page.getByRole('button').filter({ hasText: organizationName }).filter({ hasText: 'Impersonating' }).filter({ visible: true })
        ).toBeVisible();
        await openProjects(page);

        await expect(getVisibleText(page, projectName)).toBeVisible();
        await expect(getVisibleText(page, e2eScenario.projectName)).toBeHidden();

        await page.reload();
        await expect(getVisibleText(page, projectName)).toBeVisible();
        await expect(getVisibleText(page, e2eScenario.projectName)).toBeHidden();
        await page.screenshot({ path: testInfo.outputPath('impersonated-projects.png') });

        await page.getByRole('button').filter({ hasText: organizationName }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem').filter({ hasText: e2eScenario.organizationName }).click();
        await openProjects(page);
        await expect(getVisibleText(page, e2eScenario.projectName)).toBeVisible();
        await expect(getVisibleText(page, projectName)).toBeHidden();
    } finally {
        const cleanupErrors: Error[] = [];
        if (!ownerToken) {
            await runCleanupStep(cleanupErrors, 'recover impersonated owner session', async () => {
                ownerToken = await e2eApi.loginIfExists(email, E2E_TEST_PASSWORD);
            });
        }
        if (ownerToken && projectId) {
            await runCleanupStep(cleanupErrors, 'delete impersonated project', async () => {
                await e2eApi.deleteProject(ownerToken!, projectId!);
                await e2eApi.waitForProjectDeleted(ownerToken!, projectId!);
            });
        }
        if (ownerToken && organizationId) {
            await runCleanupStep(cleanupErrors, 'delete impersonated organization', async () => {
                await e2eApi.deleteOrganization(ownerToken!, organizationId!);
                await e2eApi.waitForOrganizationDeleted(ownerToken!, organizationId!);
            });
        }
        if (ownerToken) {
            await runCleanupStep(cleanupErrors, 'delete impersonated owner', async () => {
                await e2eApi.deleteCurrentUser(ownerToken!);
                await e2eApi.waitForCurrentUserDeleted(ownerToken!);
            });
        }
        throwIfCleanupFailed(cleanupErrors);
    }
});

async function openProjects(page: Page): Promise<void> {
    const projects = page.getByRole('link', { exact: true, name: 'Projects' });
    if (!(await projects.isVisible())) {
        await page.getByRole('button', { exact: true, name: 'Settings' }).click();
    }
    await projects.click();
    await expect(page.getByRole('heading', { exact: true, name: 'Projects' })).toBeVisible();
}
