import { runCleanupStep, throwIfCleanupFailed } from '../support/cleanup';
import { test as base, E2E_TEST_PASSWORD, type E2ESecondaryOrganization } from './e2e-test';

interface ImpersonatedOrganization extends E2ESecondaryOrganization {
    ownerToken: string;
}

export const test = base.extend<{ impersonatedOrganization: ImpersonatedOrganization }>({
    impersonatedOrganization: async ({ e2eApi, e2eScenario }, use) => {
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
            const projectToken = await e2eApi.getProjectDefaultToken(ownerToken, project.id);

            await use({
                message: `Impersonated event ${e2eScenario.run}`,
                organizationId,
                organizationName,
                ownerToken,
                projectId,
                projectName,
                projectToken: projectToken.id,
                referenceId: e2eScenario.referenceId
            });
        } finally {
            const errors: Error[] = [];
            if (!ownerToken) {
                await runCleanupStep(errors, 'recover impersonated owner session', async () => {
                    ownerToken = await e2eApi.loginIfExists(email, E2E_TEST_PASSWORD);
                });
            }
            if (ownerToken && projectId) {
                await runCleanupStep(errors, 'delete impersonated project', async () => {
                    await e2eApi.deleteProject(ownerToken!, projectId!);
                    await e2eApi.waitForProjectDeleted(ownerToken!, projectId!);
                });
            }
            if (ownerToken && organizationId) {
                await runCleanupStep(errors, 'delete impersonated organization', async () => {
                    await e2eApi.deleteOrganization(ownerToken!, organizationId!);
                    await e2eApi.waitForOrganizationDeleted(ownerToken!, organizationId!);
                });
            }
            if (ownerToken) {
                await runCleanupStep(errors, 'delete impersonated owner', async () => {
                    await e2eApi.deleteCurrentUser(ownerToken!);
                    await e2eApi.waitForCurrentUserDeleted(ownerToken!);
                });
            }
            throwIfCleanupFailed(errors);
        }
    }
});

export { expect } from './e2e-test';
