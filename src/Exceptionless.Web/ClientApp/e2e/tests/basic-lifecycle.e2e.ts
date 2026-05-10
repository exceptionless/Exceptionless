import type { Page } from '@playwright/test';

import { createRunName, expect, test } from '../fixtures/e2e-test';

const PASSWORD = 'tester';

test('new user can onboard a project, receive an event, and triage the stack', async ({ e2eApi, page }, testInfo) => {
    const run = createRunName(e2eApi.environment.runId, testInfo);
    const userName = `Playwright User ${run}`;
    const email = `playwright-${run}@exceptionless.test`.toLowerCase();
    const organizationName = `Playwright Org ${run}`;
    const projectName = `Playwright Project ${run}`;
    const referenceId = `pw-e2e-${run}`;
    const message = `Playwright onboarding event ${run}`;

    let organizationId: string | undefined;
    let projectId: string | undefined;
    let userToken: string | undefined;

    try {
        await test.step('sign up and create the organization in the UI', async () => {
            await page.goto('/next/signup');

            await page.getByLabel('Name').fill(userName);
            await page.getByLabel('Email').fill(email);
            await waitForEmailValidation(page);
            await page.getByLabel('Password').fill(PASSWORD);
            await page.getByRole('button', { name: 'Create My Account' }).click();

            await expect(page.getByRole('heading', { name: 'Add Organization' })).toBeVisible({ timeout: 30_000 });
            userToken = await getUserToken(page);

            await page.getByLabel('Organization Name').fill(organizationName);
            await page.getByRole('button', { name: 'Add Organization' }).click();

            await page.waitForURL(/\/next\/organization\/[^/]+\/manage/, { timeout: 30_000 });
            organizationId = getIdFromUrl(page, /\/organization\/([^/]+)\/manage/);
            await expect(page.getByRole('heading', { name: new RegExp(`${escapeRegExp(organizationName)} Settings`) })).toBeVisible();
        });

        await test.step('create the first project and verify the configure token in the UI', async () => {
            await page.getByRole('link', { name: 'add a new project' }).click();
            await expect(page.getByRole('heading', { name: 'Add Project' })).toBeVisible();

            await page.getByLabel('Project Name').fill(projectName);
            await page.getByRole('button', { name: 'Add Project' }).click();

            await page.waitForURL(/\/next\/project\/[^/]+\/configure/, { timeout: 30_000 });
            projectId = getIdFromUrl(page, /\/project\/([^/]+)\/configure/);
            await expect(page.getByRole('heading', { name: 'Download & Configure Project' })).toBeVisible();

            await selectProjectType(page, 'Bash Shell');
            await expect(page.getByText('Execute the following in your shell.')).toBeVisible();
            await expect(page.getByText(/Authorization: Bearer (?!YOUR_API_KEY)[A-Za-z0-9_-]+/)).toBeVisible();
        });

        const projectToken = await test.step('submit and index a representative event', async () => {
            expect(userToken).toBeTruthy();
            expect(projectId).toBeTruthy();

            const token = await getProjectTokenFromConfigurePage(page);

            await e2eApi.submitEvent(projectId!, token, {
                data: {
                    '@environment': {
                        machine_name: 'playwright-runner',
                        process_name: 'e2e-tests'
                    },
                    '@request': {
                        headers: {
                            'User-Agent': ['Exceptionless Playwright E2E']
                        },
                        host: 'web-ex.dev.localhost',
                        http_method: 'GET',
                        is_secure: true,
                        path: '/e2e/onboarding',
                        port: 7131,
                        query_string: {
                            reference: referenceId
                        },
                        user_agent: 'Exceptionless Playwright E2E'
                    },
                    '@simple_error': {
                        message,
                        stack_trace: `Error: ${message}\n    at onboarding-flow.spec.ts:42:13`,
                        type: 'PlaywrightOnboardingException'
                    },
                    e2e_reference: referenceId,
                    run_id: e2eApi.environment.runId
                },
                message,
                reference_id: referenceId,
                source: 'playwright-e2e',
                type: 'error'
            });

            const event = await e2eApi.pollForEventByReference(userToken!, projectId!, referenceId);
            expect(event.reference_id).toBe(referenceId);
            expect(event.stack_id).toBeTruthy();

            return { eventId: event.id, stackId: event.stack_id!, token };
        });

        await test.step('find the event in the primary views and inspect details', async () => {
            await page.goto('/next');
            await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible();
            await expect(page.getByText(message).first()).toBeVisible({ timeout: 30_000 });

            await page.goto('/next/issues');
            await expect(page.getByRole('heading', { name: 'Issues' })).toBeVisible();
            await expect(page.getByText(message).first()).toBeVisible({ timeout: 30_000 });

            await page.goto('/next/stream');
            await expect(page.getByRole('heading', { name: 'Event Stream' })).toBeVisible();
            await expect(page.getByText(message).first()).toBeVisible({ timeout: 30_000 });

            await page.goto(`/next/event/${projectToken.eventId}`);
            await expect(page.getByRole('heading', { name: 'Event Details' })).toBeVisible();
            await expect(page.getByText(message).first()).toBeVisible({ timeout: 30_000 });
            await expect(page.getByRole('tab', { name: 'Overview' })).toBeVisible();
            await expect(page.getByRole('tab', { name: 'Exception' })).toBeVisible();
            await expect(page.getByRole('tab', { name: 'Environment' })).toBeVisible();
            await expect(page.getByRole('tab', { name: 'Extended Data' })).toBeVisible();
        });

        await test.step('change stack status from the event detail view', async () => {
            expect(userToken).toBeTruthy();

            await page.getByRole('button', { exact: true, name: 'Open' }).click();
            await page.getByRole('menuitem', { name: 'Fixed' }).click();
            await expect(page.getByRole('heading', { name: 'Mark Stack As Fixed' })).toBeVisible();
            await page.getByLabel('Version').fill('1.0.0');
            await page.getByRole('button', { name: 'Mark Stack Fixed' }).click();

            await expect.poll(async () => (await e2eApi.getStack(userToken!, projectToken.stackId)).status, { timeout: 30_000 }).toBe('fixed');
            await page.reload();
            await expect(page.getByRole('button', { name: 'Fixed' })).toBeVisible({ timeout: 30_000 });
        });
    } finally {
        if (userToken && organizationId) {
            if (projectId) {
                await e2eApi.deleteProject(userToken, projectId);
            }

            await e2eApi.deleteOrganization(userToken, organizationId);
            await e2eApi.waitForOrganizationDeleted(userToken, organizationId);
        }
    }
});

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function getIdFromUrl(page: Page, pattern: RegExp): string {
    const match = pattern.exec(new URL(page.url()).pathname);
    if (!match?.[1]) {
        throw new Error(`Could not extract id from ${page.url()}`);
    }

    return match[1];
}

async function getProjectTokenFromConfigurePage(page: Page): Promise<string> {
    const text = await page.locator('body').innerText();
    const match = /Authorization: Bearer ([A-Za-z0-9_-]+)/.exec(text);
    if (!match?.[1] || match[1] === 'YOUR_API_KEY') {
        throw new Error('Configure page did not expose a project token.');
    }

    return match[1];
}

async function getUserToken(page: Page): Promise<string> {
    const token = await page.evaluate(() => window.localStorage.getItem('satellizer_token'));
    if (!token) {
        throw new Error('Signup did not persist an access token.');
    }

    return token;
}

async function selectProjectType(page: Page, optionName: string): Promise<void> {
    await page.getByRole('button', { name: /Please select a project type|Command Line:/ }).click();
    const option = page.getByRole('option', { name: optionName });

    try {
        await option.click({ timeout: 5_000 });
    } catch {
        await page.keyboard.press('Enter');
    }
}

async function waitForEmailValidation(page: Page): Promise<void> {
    await page
        .getByLabel('Validating email')
        .waitFor({ state: 'detached', timeout: 10_000 })
        .catch(() => undefined);
}
