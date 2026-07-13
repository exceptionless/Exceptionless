import { test as base, expect, type TestInfo } from '@playwright/test';

import { runCleanupStep, throwIfCleanupFailed } from '../support/cleanup';
import { E2EApiClient } from './api-client';
import { getE2EEnvironment } from './environment';

export const E2E_TEST_PASSWORD = 'tester';

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

export interface E2ESecondaryOrganization extends E2ESecondaryProject {
    organizationId: string;
    organizationName: string;
}

export interface E2ESecondaryProject {
    message: string;
    projectId: string;
    projectName: string;
    projectToken: string;
    referenceId: string;
}

interface E2EFixtures {
    e2eApi: E2EApiClient;
    e2eCleanupPassword: string;
    e2eScenario: E2EScenario;
    e2eSecondaryOrganization: E2ESecondaryOrganization;
    e2eSecondaryProject: E2ESecondaryProject;
    e2eUseGeneratedUser: boolean;
}

export const test = base.extend<E2EFixtures>({
    e2eApi: async ({ request }, use) => {
        await use(new E2EApiClient(request, getE2EEnvironment()));
    },

    e2eCleanupPassword: [E2E_TEST_PASSWORD, { option: true }],

    e2eScenario: async ({ e2eApi, e2eCleanupPassword, e2eUseGeneratedUser, page }, use, testInfo) => {
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
            if (!e2eUseGeneratedUser && !e2eApi.environment.isProduction && e2eApi.environment.email && e2eApi.environment.password) {
                userToken = await e2eApi.login();
            } else {
                userToken = await e2eApi.signup(userName, email, E2E_TEST_PASSWORD);
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
            const cleanupErrors: Error[] = [];

            if (createdUser && userToken) {
                await runCleanupStep(cleanupErrors, 'restore generated user session for cleanup', async () => {
                    userToken = await e2eApi.login(email, e2eCleanupPassword);
                });
            }

            if (userToken && projectId) {
                await runCleanupStep(cleanupErrors, `delete project ${projectId}`, async () => {
                    await e2eApi.deleteProject(userToken!, projectId!);
                    await e2eApi.waitForProjectDeleted(userToken!, projectId!);
                });
            }

            if (userToken && organizationId) {
                await runCleanupStep(cleanupErrors, `delete organization ${organizationId}`, async () => {
                    await e2eApi.deleteOrganization(userToken!, organizationId!);
                    await e2eApi.waitForOrganizationDeleted(userToken!, organizationId!);
                });
            }

            if (userToken && createdUser) {
                await runCleanupStep(cleanupErrors, 'delete generated user', async () => {
                    await e2eApi.deleteCurrentUser(userToken!);
                    await e2eApi.waitForCurrentUserDeleted(userToken!);
                });
            }

            throwIfCleanupFailed(cleanupErrors);
        }
    },

    e2eSecondaryOrganization: async ({ e2eApi, e2eScenario }, use) => {
        const organizationName = `${E2E_ORGANIZATION_NAME_PREFIX} Secondary ${e2eScenario.run}`;
        const projectName = `Playwright Secondary Organization Project ${e2eScenario.run}`;
        const referenceId = `${e2eScenario.referenceId}-organization`;
        const message = `Playwright secondary organization event ${e2eScenario.run}`;
        let organizationId: string | undefined;
        let projectId: string | undefined;

        try {
            const organization = await e2eApi.createOrganization(e2eScenario.userToken, organizationName);
            organizationId = organization.id;
            await e2eApi.waitForOrganizationListed(e2eScenario.userToken, organization.id);
            const project = await e2eApi.createProject(e2eScenario.userToken, organization.id, projectName);
            projectId = project.id;
            const projectToken = await e2eApi.getProjectDefaultToken(e2eScenario.userToken, project.id);

            await use({
                message,
                organizationId: organization.id,
                organizationName,
                projectId: project.id,
                projectName,
                projectToken: projectToken.id,
                referenceId
            });
        } finally {
            const cleanupErrors: Error[] = [];

            if (projectId) {
                await runCleanupStep(cleanupErrors, `delete secondary organization project ${projectId}`, async () => {
                    await e2eApi.deleteProject(e2eScenario.userToken, projectId!);
                    await e2eApi.waitForProjectDeleted(e2eScenario.userToken, projectId!);
                });
            }

            if (organizationId) {
                await runCleanupStep(cleanupErrors, `delete secondary organization ${organizationId}`, async () => {
                    await e2eApi.deleteOrganization(e2eScenario.userToken, organizationId!);
                    await e2eApi.waitForOrganizationDeleted(e2eScenario.userToken, organizationId!);
                });
            }

            throwIfCleanupFailed(cleanupErrors);
        }
    },

    e2eSecondaryProject: async ({ e2eApi, e2eScenario }, use) => {
        const projectName = `Playwright Secondary Project ${e2eScenario.run}`;
        const referenceId = `${e2eScenario.referenceId}-secondary`;
        const message = `Playwright secondary project event ${e2eScenario.run}`;
        let projectId: string | undefined;

        try {
            const project = await e2eApi.createProject(e2eScenario.userToken, e2eScenario.organizationId, projectName);
            projectId = project.id;
            const projectToken = await e2eApi.getProjectDefaultToken(e2eScenario.userToken, project.id);

            await use({
                message,
                projectId: project.id,
                projectName,
                projectToken: projectToken.id,
                referenceId
            });
        } finally {
            const cleanupErrors: Error[] = [];

            if (projectId) {
                await runCleanupStep(cleanupErrors, `delete secondary project ${projectId}`, async () => {
                    await e2eApi.deleteProject(e2eScenario.userToken, projectId!);
                    await e2eApi.waitForProjectDeleted(e2eScenario.userToken, projectId!);
                });
            }

            throwIfCleanupFailed(cleanupErrors);
        }
    },

    e2eUseGeneratedUser: [false, { option: true }]
});

export { expect };

export function createRunName(runId: string, testInfo: TestInfo): string {
    const rawName = [runId, `w${testInfo.workerIndex}`, `r${testInfo.retry}`, Date.now().toString(36)].join('-');
    return rawName
        .replace(/[^a-zA-Z0-9_-]/g, '-')
        .replace(/-+/g, '-')
        .slice(0, 96);
}
