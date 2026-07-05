import { expect, type Page, type TestInfo } from '@playwright/test';

import type { E2EApiClient } from '../fixtures/api-client';
import type { E2EScenario } from '../fixtures/e2e-test';

import { createRunName, E2E_ORGANIZATION_NAME_PREFIX } from '../fixtures/e2e-test';
import { runCleanupStep, throwIfCleanupFailed } from './cleanup';
import { getIdFromUrl, getProjectTokenFromConfigurePage, getUserToken, selectProjectType, waitForEmailValidation } from './page-helpers';
import { createRepresentativeEvent } from './synthetic-event';

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
        this.organizationName = `${E2E_ORGANIZATION_NAME_PREFIX} ${this.run}`;
        this.projectName = `Playwright Project ${this.run}`;
        this.referenceId = `pw-e2e-${this.run}`;
        this.message = `Playwright onboarding event ${this.run}`;
    }

    static fromScenario(page: Page, e2eApi: E2EApiClient, scenario: E2EScenario): ExceptionlessE2EJourney {
        return new ExceptionlessE2EJourney(page, e2eApi, scenario);
    }

    async cleanup(): Promise<void> {
        if (!this.userToken) {
            return;
        }

        const errors: Error[] = [];

        if (!this.organizationId) {
            await runCleanupStep(errors, `find organization ${this.organizationName}`, async () => {
                this.organizationId = await this.getOrganizationIdByName();
            });
        }

        if (this.projectId) {
            await runCleanupStep(errors, `delete project ${this.projectId}`, async () => {
                await this.e2eApi.deleteProject(this.userToken!, this.projectId!);
                await this.e2eApi.waitForProjectDeleted(this.userToken!, this.projectId!);
            });
        }

        if (this.organizationId) {
            await runCleanupStep(errors, `delete organization ${this.organizationId}`, async () => {
                await this.e2eApi.deleteOrganization(this.userToken!, this.organizationId!);
                await this.e2eApi.waitForOrganizationDeleted(this.userToken!, this.organizationId!);
            });
        }

        await runCleanupStep(errors, 'delete generated user', async () => {
            await this.e2eApi.deleteCurrentUser(this.userToken!);
            await this.e2eApi.waitForCurrentUserDeleted(this.userToken!);
        });

        throwIfCleanupFailed(errors);
    }

    async createFirstProjectAndVerifyConfigureToken(): Promise<void> {
        if (this.projectId) {
            await this.page.goto(`/next/project/${this.projectId}/configure`);
        } else {
            await this.page.getByRole('link', { name: 'add a new project' }).click();
            await expect(this.page.getByRole('heading', { name: 'Add Project' })).toBeVisible();

            await this.page.getByLabel('Project Name').fill(this.projectName);
            await this.page.getByRole('button', { name: 'Add Project' }).click();

            await this.page.waitForURL(/\/next\/project\/[^/]+\/configure/, { timeout: 30_000 });
            this.projectId = getIdFromUrl(this.page, /\/project\/([^/]+)\/configure/);
        }

        await expect(this.page.getByText('Select your project type.')).toBeVisible();

        await selectProjectType(this.page, 'Bash Shell');
        await expect(this.page.getByText('Execute the following in your shell.')).toBeVisible();
        await expect(this.page.getByText(/Authorization: Bearer (?!YOUR_API_KEY)[A-Za-z0-9_-]+/)).toBeVisible();

        this.projectToken = await getProjectTokenFromConfigurePage(this.page);
    }

    async expectEventDetails(): Promise<void> {
        expect(this.eventId).toBeTruthy();

        await this.page.goto(`/next/event/${this.eventId}`);
        await expect(this.page.getByText(this.message).first()).toBeVisible({ timeout: 30_000 });
        await expect(this.page.getByRole('tab', { name: 'Overview' })).toBeVisible();
        await expect(this.page.getByRole('tab', { name: 'Exception' })).toBeVisible();
        await expect(this.page.getByRole('tab', { name: 'Request' })).toBeVisible();
        await expect(this.page.getByRole('tab', { name: 'Environment' })).toBeVisible();
        await expect(this.page.getByRole('tab', { name: 'Extended Data' })).toBeVisible();

        await expect(this.page.getByRole('row', { name: new RegExp(`^Reference\\s+${escapeRegExp(this.referenceId)}$`) })).toBeVisible();
        await expect(this.page.getByRole('row').filter({ hasText: 'Source' }).filter({ hasText: 'playwright-e2e' })).toBeVisible();
        await expect(this.page.getByRole('row').filter({ hasText: 'Error Type' }).filter({ hasText: 'PlaywrightOnboardingException' }).first()).toBeVisible();

        await this.page.getByRole('tab', { name: 'Exception' }).click();
        await expect(this.page.getByRole('row').filter({ hasText: 'Message' }).filter({ hasText: this.message })).toBeVisible();
        await expect(this.page.getByText('exceptionless-journey.ts:42:13')).toBeVisible();

        await this.page.getByRole('tab', { name: 'Request' }).click();
        await expect(this.page.getByRole('row').filter({ hasText: 'HTTP Method' }).filter({ hasText: 'GET' })).toBeVisible();
        await expect(this.page.getByRole('row').filter({ hasText: 'URL' }).filter({ hasText: '/e2e/onboarding' })).toBeVisible();
        await expect(this.page.getByRole('row').filter({ hasText: 'User Agent' }).filter({ hasText: 'Exceptionless Playwright E2E' })).toBeVisible();

        await this.page.getByRole('tab', { name: 'Environment' }).click();
        await expect(this.page.getByRole('row').filter({ hasText: 'Machine Name' }).filter({ hasText: 'playwright-runner' })).toBeVisible();
        await expect(this.page.getByRole('row').filter({ hasText: 'Process Name' }).filter({ hasText: 'e2e-tests' })).toBeVisible();

        await this.page.getByRole('tab', { name: 'Extended Data' }).click();
        await expect(this.page.getByText('e2e_reference')).toBeVisible();
        await expect(this.page.getByText(this.referenceId).first()).toBeVisible();
        await expect(this.page.getByText('run_id')).toBeVisible();
        await expect(this.page.getByText(this.e2eApi.environment.runId).first()).toBeVisible();
    }

    async expectEventInPrimaryViews(): Promise<void> {
        await this.page.goto('/next/event');
        await expect(this.page.getByRole('heading', { name: 'Events' })).toBeVisible();
        await expect(this.page.getByText(this.message).first()).toBeVisible({ timeout: 30_000 });

        await this.page.goto('/next/stack');
        await expect(this.page.getByRole('heading', { name: 'Stacks' })).toBeVisible();
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
        const setupHeading = this.page.getByText('Set Up Exceptionless');

        if (!(await setupHeading.isVisible())) {
            await this.page.goto('/next/organization/add');
        }

        await expect(setupHeading).toBeVisible({ timeout: 30_000 });

        await this.page.getByLabel('Organization Name').fill(this.organizationName);
        await this.page.getByLabel('Project Name').fill(this.projectName);
        await this.page.getByRole('button', { name: 'Continue' }).click();

        await this.page.waitForURL(/\/next\/project\/[^/]+\/configure/, { timeout: 30_000 });
        this.projectId = getIdFromUrl(this.page, /\/project\/([^/]+)\/configure/);
        this.organizationId = await this.getOrganizationIdByName();
        await expect(this.page.getByText('Select your project type.')).toBeVisible();
    }

    async submitRepresentativeEvent(): Promise<void> {
        expect(this.projectId).toBeTruthy();
        expect(this.projectToken).toBeTruthy();
        expect(this.userToken).toBeTruthy();

        await this.e2eApi.submitEvent(
            this.projectId!,
            this.projectToken!,
            createRepresentativeEvent({
                appUrl: this.e2eApi.environment.appUrl,
                message: this.message,
                referenceId: this.referenceId,
                runId: this.e2eApi.environment.runId
            })
        );

        const event = await this.e2eApi.pollForEventByReference(this.userToken!, this.projectId!, this.referenceId);
        expect(event.reference_id).toBe(this.referenceId);
        expect(event.stack_id).toBeTruthy();

        this.eventId = event.id;
        this.stackId = event.stack_id;
    }

    private async getOrganizationIdByName(): Promise<string> {
        expect(this.userToken).toBeTruthy();

        await expect
            .poll(async () => (await this.e2eApi.getOrganizations(this.userToken!)).find((item) => item.name === this.organizationName)?.id, {
                timeout: 30_000
            })
            .toBeTruthy();

        const organization = (await this.e2eApi.getOrganizations(this.userToken!)).find((item) => item.name === this.organizationName);
        if (!organization?.id) {
            throw new Error(`Could not find organization ${this.organizationName}`);
        }

        return organization.id;
    }
}

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function isE2EScenario(value: E2EScenario | TestInfo): value is E2EScenario {
    return 'projectToken' in value;
}
