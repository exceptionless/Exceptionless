import { test as base, expect, type Page, type TestInfo } from '@playwright/test';

import { E2EApiClient, type E2EEvent, type E2EOrganization, type E2EProject, type E2EToken } from './api-client';
import { getE2EEnvironment } from './environment';

export interface E2EScenario {
    event: E2EEvent & { message: string; reference_id: string };
    organization: E2EOrganization;
    project: E2EProject;
    projectToken: E2EToken;
    referenceId: string;
    runName: string;
    userToken: string;
}

interface E2EFixtures {
    authenticatedPage: Page;
    e2eApi: E2EApiClient;
    e2eScenario: E2EScenario;
}

export const test = base.extend<E2EFixtures>({
    authenticatedPage: async ({ e2eScenario, page }, use) => {
        await page.addInitScript(
            ({ organizationId, token }) => {
                window.localStorage.setItem('satellizer_token', token);
                window.localStorage.setItem('organization', JSON.stringify(organizationId));
            },
            {
                organizationId: e2eScenario.organization.id,
                token: e2eScenario.userToken
            }
        );

        await use(page);
    },

    e2eApi: async ({ request }, use) => {
        await use(new E2EApiClient(request, getE2EEnvironment()));
    },

    e2eScenario: async ({ e2eApi }, use, testInfo) => {
        const userToken = await e2eApi.login();
        const runName = createRunName(e2eApi.environment.runId, testInfo);
        const organization = await e2eApi.createOrganization(userToken, `Playwright E2E ${runName}`);

        try {
            const project = await e2eApi.createProject(userToken, organization.id, `E2E Project ${runName}`);
            const projectToken = await e2eApi.getProjectDefaultToken(userToken, project.id);
            const referenceId = `pw-e2e-${runName}`;
            const message = `Playwright E2E event ${runName}`;

            await e2eApi.submitEvent(project.id, projectToken.id, {
                data: {
                    e2e_reference: referenceId,
                    run_id: e2eApi.environment.runId
                },
                message,
                reference_id: referenceId,
                source: 'playwright-e2e',
                type: 'error'
            });

            const event = await e2eApi.pollForEventByReference(userToken, project.id, referenceId);

            await use({
                event: {
                    ...event,
                    message,
                    reference_id: referenceId
                },
                organization,
                project,
                projectToken,
                referenceId,
                runName,
                userToken
            });
        } finally {
            await e2eApi.deleteOrganization(userToken, organization.id);
            await e2eApi.waitForOrganizationDeleted(userToken, organization.id);
        }
    }
});

export { expect };

function createRunName(runId: string, testInfo: TestInfo): string {
    const rawName = [runId, `w${testInfo.workerIndex}`, `r${testInfo.retry}`, Date.now().toString(36)].join('-');
    return rawName
        .replace(/[^a-zA-Z0-9_-]/g, '-')
        .replace(/-+/g, '-')
        .slice(0, 96);
}
