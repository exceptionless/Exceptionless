import { test as base, expect, type TestInfo } from '@playwright/test';

import { E2EApiClient } from './api-client';
import { getE2EEnvironment } from './environment';

const PASSWORD = 'tester';

export const E2E_ORGANIZATION_NAME_PREFIX = 'E2E Playwright Org';

export interface E2EScenario {
    email: string;
    message: string;
    organizationId: string;
    organizationName: string;
    projectId: string;
    projectName: string;
    projectToken: string;
    referenceId: string;
    run: string;
    userName: string;
    userToken: string;
}

interface E2EFixtures {
    e2eApi: E2EApiClient;
    e2eScenario: E2EScenario;
}

export const test = base.extend<E2EFixtures>({
    e2eApi: async ({ request }, use) => {
        await use(new E2EApiClient(request, getE2EEnvironment()));
    },

    e2eScenario: async ({ e2eApi, page }, use, testInfo) => {
        const run = createRunName(e2eApi.environment.runId, testInfo);
        const userName = `Playwright User ${run}`;
        const email = `playwright-${run}@exceptionless.test`.toLowerCase();
        const organizationName = `${E2E_ORGANIZATION_NAME_PREFIX} ${run}`;
        const projectName = `Playwright Project ${run}`;
        const referenceId = `pw-e2e-${run}`;
        const message = `Playwright onboarding event ${run}`;
        let organizationId: string | undefined;
        let projectId: string | undefined;
        let userToken: string | undefined;
        let createdUser = false;

        try {
            if (e2eApi.environment.email && e2eApi.environment.password) {
                userToken = await e2eApi.login();
            } else {
                userToken = await e2eApi.signup(userName, email, PASSWORD);
                createdUser = true;
            }

            const organization = await e2eApi.createOrganization(userToken, organizationName);
            organizationId = organization.id;
            const project = await e2eApi.createProject(userToken, organization.id, projectName);
            projectId = project.id;
            const projectToken = await e2eApi.getProjectDefaultToken(userToken, project.id);

            await page.addInitScript(
                ({ organizationId, token }) => {
                    window.localStorage.setItem('satellizer_token', token);
                    window.localStorage.setItem('organization', JSON.stringify(organizationId));
                },
                { organizationId: organization.id, token: userToken }
            );

            await use({
                email,
                message,
                organizationId: organization.id,
                organizationName,
                projectId: project.id,
                projectName,
                projectToken: projectToken.id,
                referenceId,
                run,
                userName,
                userToken
            });
        } finally {
            if (userToken && projectId) {
                await e2eApi.deleteProject(userToken, projectId);
                await e2eApi.waitForProjectDeleted(userToken, projectId);
            }

            if (userToken && organizationId) {
                await e2eApi.deleteOrganization(userToken, organizationId);
                await e2eApi.waitForOrganizationDeleted(userToken, organizationId);
            }

            if (userToken && createdUser) {
                await e2eApi.deleteCurrentUser(userToken);
                await e2eApi.waitForCurrentUserDeleted(userToken);
            }
        }
    }
});

export { expect };

export function createRunName(runId: string, testInfo: TestInfo): string {
    const rawName = [runId, `w${testInfo.workerIndex}`, `r${testInfo.retry}`, Date.now().toString(36)].join('-');
    return rawName
        .replace(/[^a-zA-Z0-9_-]/g, '-')
        .replace(/-+/g, '-')
        .slice(0, 96);
}
