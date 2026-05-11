import { expect, type Page, type TestInfo } from '@playwright/test';

import type { E2EApiClient } from '../fixtures/api-client';
import type { E2EScenario } from '../fixtures/e2e-test';

import { createRunName } from '../fixtures/e2e-test';

const FIXED_VERSION = '1.0.0';
const PASSWORD = 'tester';

export class ExceptionlessE2EJourney {
    email: string;
    eventId?: string;
    message: string;
    organizationId?: string;
    organizationName: string;
    projectId?: string;
    projectName: string;
    projectToken?: string;
    referenceId: string;
    run: string;
    stackId?: string;
    userName: string;
    userToken?: string;

    constructor(
        private readonly page: Page,
        private readonly e2eApi: E2EApiClient,
        testInfoOrScenario: E2EScenario | TestInfo
    ) {
        if (isE2EScenario(testInfoOrScenario)) {
            this.email = testInfoOrScenario.email;
            this.message = testInfoOrScenario.message;
            this.organizationId = testInfoOrScenario.organizationId;
            this.organizationName = testInfoOrScenario.organizationName;
            this.projectId = testInfoOrScenario.projectId;
            this.projectName = testInfoOrScenario.projectName;
            this.projectToken = testInfoOrScenario.projectToken;
            this.referenceId = testInfoOrScenario.referenceId;
            this.run = testInfoOrScenario.run;
            this.userName = testInfoOrScenario.userName;
            this.userToken = testInfoOrScenario.userToken;

            return;
        }

        const testInfo = testInfoOrScenario;
        this.run = createRunName(e2eApi.environment.runId, testInfo);
        this.userName = `Playwright User ${this.run}`;
        this.email = `playwright-${this.run}@exceptionless.test`.toLowerCase();
        this.organizationName = `Playwright Org ${this.run}`;
        this.projectName = `Playwright Project ${this.run}`;
        this.referenceId = `pw-e2e-${this.run}`;
        this.message = `Playwright onboarding event ${this.run}`;
    }

    static fromScenario(page: Page, e2eApi: E2EApiClient, scenario: E2EScenario): ExceptionlessE2EJourney {
        return new ExceptionlessE2EJourney(page, e2eApi, scenario);
    }

    async cleanup(): Promise<void> {
        if (!this.userToken || !this.organizationId) {
            return;
        }

        if (this.projectId) {
            await this.e2eApi.deleteProject(this.userToken, this.projectId);
        }

        await this.e2eApi.deleteOrganization(this.userToken, this.organizationId);
        await this.e2eApi.waitForOrganizationDeleted(this.userToken, this.organizationId);
    }

    async createFirstProjectAndVerifyConfigureToken(): Promise<void> {
        await this.page.getByRole('link', { name: 'add a new project' }).click();
        await expect(this.page.getByRole('heading', { name: 'Add Project' })).toBeVisible();

        await this.page.getByLabel('Project Name').fill(this.projectName);
        await this.page.getByRole('button', { name: 'Add Project' }).click();

        await this.page.waitForURL(/\/next\/project\/[^/]+\/configure/, { timeout: 30_000 });
        this.projectId = getIdFromUrl(this.page, /\/project\/([^/]+)\/configure/);
        await expect(this.page.getByRole('heading', { name: 'Download & Configure Project' })).toBeVisible();

        await selectProjectType(this.page, 'Bash Shell');
        await expect(this.page.getByText('Execute the following in your shell.')).toBeVisible();
        await expect(this.page.getByText(/Authorization: Bearer (?!YOUR_API_KEY)[A-Za-z0-9_-]+/)).toBeVisible();

        this.projectToken = await getProjectTokenFromConfigurePage(this.page);
    }

    async expectEventDetails(): Promise<void> {
        expect(this.eventId).toBeTruthy();

        await this.page.goto('/next');
        await this.page
            .getByRole('link', { name: new RegExp(escapeRegExp(this.message)) })
            .first()
            .click();
        const eventDetails = this.page.getByRole('dialog', { name: 'Event Details' });
        await expect(eventDetails).toBeVisible();
        await expect(eventDetails.getByText(this.message).first()).toBeVisible({ timeout: 30_000 });
        await expect(eventDetails.getByRole('tab', { name: 'Overview' })).toBeVisible();
        await expect(eventDetails.getByRole('tab', { name: 'Exception' })).toBeVisible();
        await expect(eventDetails.getByRole('tab', { name: 'Environment' })).toBeVisible();
        await expect(eventDetails.getByRole('tab', { name: 'Extended Data' })).toBeVisible();
    }

    async expectEventInPrimaryViews(): Promise<void> {
        await this.page.goto('/next');
        await expect(this.page.getByRole('heading', { name: 'Events' })).toBeVisible();
        await expect(this.page.getByText(this.message).first()).toBeVisible({ timeout: 30_000 });

        await this.page.goto('/next/issues');
        await expect(this.page.getByRole('heading', { name: 'Issues' })).toBeVisible();
        await expect(this.page.getByText(this.message).first()).toBeVisible({ timeout: 30_000 });

        await this.page.goto('/next/stream');
        await expect(this.page.getByRole('heading', { name: 'Event Stream' })).toBeVisible();
        await expect(this.page.getByText(this.message).first()).toBeVisible({ timeout: 30_000 });
    }

    async markStackFixed(version = FIXED_VERSION): Promise<void> {
        expect(this.stackId).toBeTruthy();
        expect(this.userToken).toBeTruthy();

        await this.expectEventDetails();
        await this.page.getByRole('button', { exact: true, name: 'Open' }).click();
        await this.page.getByRole('menuitem', { name: 'Fixed' }).click();
        await expect(this.page.getByRole('heading', { name: 'Mark Stack As Fixed' })).toBeVisible();
        await this.page.getByLabel('Version').fill(version);
        await this.page.getByRole('button', { name: 'Mark Stack Fixed' }).click();

        await expect.poll(async () => (await this.e2eApi.getStack(this.userToken!, this.stackId!)).status, { timeout: 30_000 }).toBe('fixed');
        await expect(this.page.getByRole('button', { name: 'Fixed' })).toBeVisible({ timeout: 30_000 });
    }

    async onboardProject(): Promise<void> {
        await this.signUpAndCreateOrganization();
        await this.createFirstProjectAndVerifyConfigureToken();
    }

    async signUpAndCreateOrganization(): Promise<void> {
        await this.page.goto('/next/signup');

        await this.page.getByLabel('Name').fill(this.userName);
        await this.page.getByLabel('Email').fill(this.email);
        await waitForEmailValidation(this.page);
        await this.page.getByLabel('Password').fill(PASSWORD);
        await this.page.getByRole('button', { name: 'Create My Account' }).click();

        this.userToken = await getUserToken(this.page);
        const addOrganizationHeading = this.page.getByRole('heading', { name: 'Add Organization' });

        if (!(await addOrganizationHeading.isVisible())) {
            await this.page.goto('/next/organization/add');
        }

        await expect(addOrganizationHeading).toBeVisible({ timeout: 30_000 });

        await this.page.getByLabel('Organization Name').fill(this.organizationName);
        await this.page.getByRole('button', { name: 'Add Organization' }).click();

        await this.page.waitForURL(/\/next\/organization\/[^/]+\/manage/, { timeout: 30_000 });
        this.organizationId = getIdFromUrl(this.page, /\/organization\/([^/]+)\/manage/);
        await expect(this.page.getByRole('heading', { name: new RegExp(`${escapeRegExp(this.organizationName)} Settings`) })).toBeVisible();
    }

    async submitRepresentativeEvent(): Promise<void> {
        expect(this.projectId).toBeTruthy();
        expect(this.projectToken).toBeTruthy();
        expect(this.userToken).toBeTruthy();

        await this.e2eApi.submitEvent(this.projectId!, this.projectToken!, {
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
                        reference: this.referenceId
                    },
                    user_agent: 'Exceptionless Playwright E2E'
                },
                '@simple_error': {
                    message: this.message,
                    stack_trace: `Error: ${this.message}\n    at exceptionless-journey.ts:42:13`,
                    type: 'PlaywrightOnboardingException'
                },
                e2e_reference: this.referenceId,
                run_id: this.e2eApi.environment.runId
            },
            message: this.message,
            reference_id: this.referenceId,
            source: 'playwright-e2e',
            type: 'error'
        });

        const event = await this.e2eApi.pollForEventByReference(this.userToken!, this.projectId!, this.referenceId);
        expect(event.reference_id).toBe(this.referenceId);
        expect(event.stack_id).toBeTruthy();

        this.eventId = event.id;
        this.stackId = event.stack_id;
    }
}

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
    await expect.poll(async () => page.evaluate(() => window.localStorage.getItem('satellizer_token')), { timeout: 30_000 }).toBeTruthy();
    const token = await page.evaluate(() => window.localStorage.getItem('satellizer_token'));
    if (!token) {
        throw new Error('Signup did not persist an access token.');
    }

    return token;
}

function isE2EScenario(value: E2EScenario | TestInfo): value is E2EScenario {
    return 'projectToken' in value;
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
