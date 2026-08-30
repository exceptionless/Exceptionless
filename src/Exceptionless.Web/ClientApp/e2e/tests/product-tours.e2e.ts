import type { Page, Request, Response } from '@playwright/test';

import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { createRepresentativeEvent } from '../support/synthetic-event';

test.use({ actionTimeout: 15_000, e2eUseGeneratedUser: true });

test.describe('first-run welcome', () => {
    test.use({ e2eDismissProductTourWelcome: false });

    test('Browse Guides persists before the catalog opens', async ({ e2eScenario, page }) => {
        await test.step(`show the first-run prompt for ${e2eScenario.email}`, async () => {
            await page.goto('/next/stack');
            await expect(page.getByRole('dialog', { name: 'Welcome to Exceptionless' })).toBeVisible();
        });

        const persisted = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
        await page.getByRole('dialog', { name: 'Welcome to Exceptionless' }).getByRole('button', { name: 'Browse Guides' }).click();
        await persisted;

        const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
        await expect(catalog).toBeVisible();
        await catalog.getByRole('button', { name: 'Close' }).click();
        await page.reload();
        await expect(page.getByRole('dialog', { name: 'Welcome to Exceptionless' })).toBeHidden();
    });
});

test.describe('shell and identity checkpoints', () => {
    test.use({ e2eDismissProductTourWelcome: false });

    test('supports responsive resume and never carries checkpoints across identities', async ({ e2eApi, e2eScenario, e2eSecondaryOrganization, page }) => {
        test.setTimeout(240_000);
        const progressWrites: string[] = [];
        page.on('request', (request) => {
            if (request.method() === 'PUT' && request.url().includes('/api/v2/users/me/product-tours/')) {
                progressWrites.push(new URL(request.url()).pathname);
            }
        });

        await test.step('closing the welcome persists dismissal', async () => {
            await page.goto('/next/stack');
            await expect(page.getByRole('dialog', { name: 'Welcome to Exceptionless' })).toBeVisible();
            const dismissed = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
            await page.keyboard.press('Escape');
            await dismissed;
            await expect(page.getByRole('dialog', { name: 'Welcome to Exceptionless' })).toBeHidden();
        });

        await test.step('the shell tour renders on mobile and resumes on desktop with reduced motion', async () => {
            await page.setViewportSize({ height: 844, width: 390 });
            await startTourFromCommand(page, 'Explore Exceptionless');
            const tour = page.locator('.driver-popover');
            await expect(page.locator('[data-tour="app-navigation"]')).toBeVisible();
            await expect(tour.getByText('Your workspace navigation')).toBeVisible();

            await page.emulateMedia({ reducedMotion: 'reduce' });
            await page.setViewportSize({ height: 900, width: 1440 });
            await tour.getByRole('button', { name: 'Continue' }).click();
            await expect(tour.getByText('Find anything quickly')).toBeVisible();
            await page.reload();
            await expect(tour.getByText('Find anything quickly')).toBeVisible();

            const dismissed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
            await tour.getByRole('button', { name: 'End guide' }).click();
            await dismissed;
            await expectProductTourSession(page, false);
        });

        await test.step('every shell target remains visible on mobile', async () => {
            await mockAssistantAccess(page);
            await page.reload();
            await page.setViewportSize({ height: 844, width: 390 });
            await startTourFromCommand(page, 'Explore Exceptionless');
            const tour = page.locator('.driver-popover');

            for (const [title, target] of [
                ['Your workspace navigation', '[data-tour="app-navigation"]'],
                ['Find anything quickly', '[data-tour="command-search"]'],
                ['Reuse configured views', '[data-tour="saved-view-navigation"]'],
                ['Ask Exie with context', '[data-tour="exie-trigger"]'],
                ['Help is always nearby', '[data-tour="help-menu"]']
            ] as const) {
                await expect(tour.getByText(title)).toBeVisible();
                await expect(page.locator(target)).toBeVisible();
                if (title !== 'Help is always nearby') {
                    await tour.getByRole('button', { name: 'Continue' }).click();
                }
            }

            const completed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
            await tour.getByRole('button', { name: 'Continue' }).click();
            await completed;
            await expectProductTourSession(page, false);
        });

        await test.step('an organization change clears an active checkpoint without recording progress', async () => {
            await mockAssistantAccess(page);
            await page.reload();
            await startTourFromCommand(page, 'Meet Exie');
            await expectProductTourSession(page, true);
            const writesBeforeSwitch = progressWrites.length;

            const identityTab = await page.context().newPage();
            await identityTab.goto('/next/stack');
            await identityTab.evaluate((organizationId) => {
                window.localStorage.setItem('organization', JSON.stringify(organizationId));
            }, e2eSecondaryOrganization.organizationId);
            await identityTab.close();
            await expectProductTourSession(page, false);
            expect(progressWrites).toHaveLength(writesBeforeSwitch);
        });

        await test.step('logout clears an active checkpoint without recording progress', async () => {
            await page.setViewportSize({ height: 900, width: 1440 });
            await startTourFromCommand(page, 'Meet Exie');
            await expectProductTourSession(page, true);
            const writesBeforeLogout = progressWrites.length;

            await page.getByRole('button', { name: new RegExp(e2eScenario.userName) }).dispatchEvent('click');
            await page.getByRole('menuitem', { name: 'Log Out' }).dispatchEvent('click');
            await expect(page).toHaveURL(/\/next\/login/);
            await expectProductTourSession(page, false);
            expect(progressWrites).toHaveLength(writesBeforeLogout);

            e2eScenario.userToken = await e2eApi.login(e2eScenario.email, E2E_TEST_PASSWORD);
        });
    });
});

test('domain workflows advance only on real success', async ({ e2eApi, e2eScenario, page }) => {
    test.setTimeout(300_000);

    await test.step('project configuration advances after setup and the first event', async () => {
        await page.goto('/next/stack');
        await startTourFromCommand(page, 'Configure a project');
        await page.waitForURL(/\/next\/project\/(?:add|[^/]+\/configure)/);

        let createdProject = false;
        let projectId = page.url().match(/\/project\/([^/]+)\/configure/)?.[1];
        if (!projectId) {
            createdProject = true;
            await expect(page.getByRole('heading', { name: 'Add Project' })).toBeVisible();
            await page.getByLabel('Project Name', { exact: true }).fill(`Tour Project ${e2eScenario.run}`);
            await page.getByRole('button', { name: 'Continue to Client Setup' }).click();
            await page.waitForURL(/\/next\/project\/[^/]+\/configure\?redirect=true/);
            projectId = page.url().match(/\/project\/([^/]+)\/configure/)?.[1];
        } else {
            expect(projectId).toBe(e2eScenario.projectId);
        }

        expect(projectId).toBeTruthy();

        await page.locator('[data-tour="project-configure-platform"]').click();
        await page.getByRole('option', { name: 'Browser applications' }).click();
        await page.locator('[data-product-tour-inline="project-configure"]').getByRole('button', { name: 'Continue' }).click();
        await expect(page.getByText('Waiting for your first event')).toBeVisible();

        let projectProgressRequests = 0;
        const projectProgressRoute = (url: URL) => url.pathname === '/api/v2/users/me/product-tours/project-configure';
        await page.route(projectProgressRoute, async (route) => {
            projectProgressRequests += 1;
            await route.fulfill({ json: { title: 'Injected progress failure' }, status: 500 });
        });
        try {
            const token = await e2eApi.getProjectDefaultToken(e2eScenario.userToken, projectId!);
            await e2eApi.submitEvent(
                projectId!,
                token.id,
                createRepresentativeEvent({
                    appUrl: e2eApi.environment.appUrl,
                    message: e2eScenario.message,
                    referenceId: e2eScenario.referenceId,
                    runId: e2eApi.environment.runId
                })
            );
            await expect(page).toHaveURL(/\/next\/event/);
            await expectProductTourSession(page, true);
            await expect.poll(() => projectProgressRequests).toBe(1);
            await expect.poll(async () => (await e2eApi.getProject(e2eScenario.userToken, projectId!))?.is_configured).toBe(true);

            await page.unroute(projectProgressRoute);
            const completed = page.waitForResponse(isSuccessfulTourProgress('project-configure'));
            await page.goto(`/next/project/${projectId}/configure`);
            await completed;
            await expectProductTourSession(page, false);
        } finally {
            await page.unroute(projectProgressRoute);
            if (createdProject) {
                await e2eApi.deleteProject(e2eScenario.userToken, projectId!);
                await e2eApi.waitForProjectDeleted(e2eScenario.userToken, projectId!);
            }
        }
    });

    await test.step('saved-view progress retry never repeats the successful POST', async () => {
        let createRequests = 0;
        let progressRequests = 0;
        const countSavedViewCreation = (request: Request) => {
            const path = new URL(request.url()).pathname;
            if (request.method() === 'POST' && /^\/api\/v2\/organizations\/[^/]+\/saved-views$/.test(path)) createRequests += 1;
        };
        const progressRoute = (url: URL) => url.pathname === '/api/v2/users/me/product-tours/saved-view-create';
        page.on('request', countSavedViewCreation);
        await page.route(progressRoute, async (route) => {
            progressRequests += 1;
            if (progressRequests === 1) {
                await route.fulfill({ json: { title: 'Injected progress failure' }, status: 500 });
                return;
            }

            await route.continue();
        });

        try {
            await page.goto('/next/event');
            await startTourFromCommand(page, 'Create a saved view');
            await expectProductTourSession(page, true);
            const tour = page.locator('.driver-popover');
            await tour.getByRole('button', { name: 'Continue' }).click();
            await tour.getByRole('button', { name: 'Continue' }).click();

            await page.getByLabel('Name', { exact: true }).fill(`Tour View ${e2eScenario.run}`);
            await page.getByRole('button', { name: 'Continue' }).click();
            await page.getByRole('button', { name: 'Continue' }).click();
            await page.getByRole('button', { exact: true, name: 'Save' }).click();
            await expect(page.getByText('Retry guide completion')).toBeVisible();
            expect(createRequests).toBe(1);

            await page.reload();
            await expect(page.getByRole('button', { name: 'Retry guide completion' })).toBeVisible();
            const completed = page.waitForResponse(isSuccessfulTourProgress('saved-view-create'));
            await page.getByRole('button', { name: 'Retry guide completion' }).click();
            await completed;
            await expect.poll(() => createRequests).toBe(1);
            await expectProductTourSession(page, false);
        } finally {
            page.off('request', countSavedViewCreation);
            await page.unroute(progressRoute);
        }
    });

    await test.step('investigation advances when a real error opens', async () => {
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: e2eScenario.message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: e2eScenario.referenceId
        });
        await page.goto('/next/event?time=all&type=error');
        await expect(page.getByText(e2eScenario.message).first()).toBeVisible({ timeout: 30_000 });
        await startTourFromCommand(page, 'Investigate an error');
        await page.locator('.driver-popover').getByRole('button', { name: 'Continue' }).click();
        await page.locator('tr').filter({ hasText: e2eScenario.message }).first().click();
        const callout = page.locator('[data-product-tour-inline="event-investigate"]');
        await expect(callout.getByText('Understand the grouped issue')).toBeVisible();
        for (const title of ['Triage deliberately', 'Inspect the occurrence', 'Begin with the overview', 'Compare every occurrence']) {
            await callout.getByRole('button', { name: 'Continue' }).click();
            await expect(callout.getByText(title)).toBeVisible();
        }

        const completed = page.waitForResponse(isSuccessfulTourProgress('event-investigate'));
        await callout.getByRole('button', { name: 'Finish guide' }).click();
        await completed;
        await expectProductTourSession(page, false);
        await page.reload();
        await expect(page.locator('[data-product-tour-inline="event-investigate"]')).toBeHidden();
    });

    await test.step('Exie opens context without provider submission', async () => {
        await mockAssistantAccess(page);
        let chatRequests = 0;
        const countChatRequest = (request: Request) => {
            if (new URL(request.url()).pathname === '/api/v2/assistant/chat') chatRequests += 1;
        };
        page.on('request', countChatRequest);

        try {
            await page.goto('/next/stack');
            await startTourFromCommand(page, 'Meet Exie');
            const tour = page.locator('.driver-popover');
            await tour.getByRole('button', { name: 'Continue' }).click();
            await expect(tour.getByText('You control every request')).toBeVisible();
            expect(chatRequests).toBe(0);
        } finally {
            page.off('request', countChatRequest);
        }
    });
});

async function expectProductTourSession(page: Page, present: boolean): Promise<void> {
    const assertion = expect.poll(() => page.evaluate(() => sessionStorage.getItem('exceptionless.product-tour')));
    if (present) {
        await assertion.not.toBeNull();
    } else {
        await assertion.toBeNull();
    }
}

function isSuccessfulTourProgress(tourName: string) {
    return (response: Response): boolean => {
        const path = new URL(response.url()).pathname;
        return response.request().method() === 'PUT' && path === `/api/v2/users/me/product-tours/${tourName}` && response.status() === 200;
    };
}

async function mockAssistantAccess(page: Page): Promise<void> {
    await page.route(
        (url) => url.pathname === '/api/v2/assistant/access',
        (route) => route.fulfill({ json: { enabled: true, has_access: true, message: null, upgrade_required: false } })
    );
}

async function startTourFromCommand(page: Page, title: string): Promise<void> {
    const announcementStart = page.getByRole('button', { name: 'See how it works' });
    if (title === 'Meet Exie' && (await announcementStart.isVisible())) {
        await announcementStart.click();
        return;
    }

    await page.getByRole('button', { name: 'Search Exceptionless' }).click();
    await page.getByRole('dialog').getByText('Guided Tours…', { exact: true }).click();
    const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
    const tour = catalog.getByRole('region', { name: title });
    await tour.getByRole('button', { name: /^(Continue|Restart|Start)$/ }).click();
}
