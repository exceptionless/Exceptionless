import type { Page, Request, Response } from '@playwright/test';

import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { createRepresentativeEvent } from '../support/synthetic-event';

test.use({ actionTimeout: 15_000, e2eUseInvitedUser: true });

test.describe('first-run welcome', () => {
    test.use({ e2eDismissProductTourWelcome: false });

    test('Browse Guides persists before the catalog opens', async ({ e2eScenario, page }, testInfo) => {
        await test.step(`show the first-run prompt for ${e2eScenario.email}`, async () => {
            await page.goto('/next/stack');
            await expect(page.getByRole('region', { name: 'Welcome to Exceptionless' })).toBeVisible();
            await expect(page.getByRole('dialog')).toBeHidden();
            await page.screenshot({ path: testInfo.outputPath('welcome-desktop.png') });
            await page.getByRole('button', { name: 'Search Exceptionless' }).click();
            await expect(page.getByRole('dialog')).toBeVisible();
            await page.keyboard.press('Escape');
            await expect(page.getByRole('region', { name: 'Welcome to Exceptionless' })).toBeVisible();
        });

        const persisted = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
        await page.getByRole('region', { name: 'Welcome to Exceptionless' }).getByRole('button', { name: 'Browse guides' }).click();
        await persisted;

        const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
        await expect(catalog).toBeVisible();
        await catalog.getByRole('button', { name: 'Close' }).click();
        await page.reload();
        await expect(page.getByRole('region', { name: 'Welcome to Exceptionless' })).toBeHidden();
    });

    test('the compact mobile welcome respects reduced motion and starts the recommended setup', async ({ e2eScenario, page }, testInfo) => {
        // Arrange
        await page.setViewportSize({ height: 844, width: 390 });
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.goto('/next/stack');
        const welcome = page.getByRole('region', { name: 'Welcome to Exceptionless' });
        await expect(welcome).toBeVisible();

        // Act
        const presentation = await welcome.evaluate((element) => {
            const bounds = element.getBoundingClientRect();
            return { animation: getComputedStyle(element).animationName, bottom: bounds.bottom, height: bounds.height, left: bounds.left, right: bounds.right };
        });

        // Assert
        expect(presentation.animation).toBe('none');
        expect(presentation.left).toBeGreaterThanOrEqual(16);
        expect(presentation.right).toBeLessThanOrEqual(374);
        expect(presentation.bottom).toBeLessThanOrEqual(828);
        expect(presentation.height).toBeLessThan(220);
        await expect(page.getByRole('dialog')).toBeHidden();
        await page.screenshot({ path: testInfo.outputPath('welcome-mobile.png') });
        const persisted = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
        await welcome.getByRole('button', { name: 'Continue setup' }).click();
        await persisted;
        await expect(page).toHaveURL(new RegExp(`/next/project/(?:add|${e2eScenario.projectId}/configure)`));
        await expect(welcome).toBeHidden();
    });

    test('a failed close remains retryable and successful dismissal survives reload', async ({ e2eScenario, page }) => {
        // Arrange
        const welcome = page.getByRole('region', { name: 'Welcome to Exceptionless' });
        await test.step(`show the welcome for ${e2eScenario.email}`, async () => {
            await page.goto('/next/stack');
            await expect(welcome).toBeVisible();
        });
        const progressRoute = '**/api/v2/users/me/product-tours/app-welcome';
        await page.route(progressRoute, (route) => route.fulfill({ json: { title: 'Injected progress failure' }, status: 500 }));

        // Act
        await welcome.getByRole('button', { name: 'Close welcome' }).click();

        // Assert
        await expect(page.getByText('We could not save your guided-tour preference. Please try again.')).toBeVisible();
        await expect(welcome).toBeVisible();
        await page.unroute(progressRoute);
        const persisted = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
        await welcome.getByRole('button', { name: 'Close welcome' }).click();
        await persisted;
        await page.reload();
        await expect(welcome).toBeHidden();
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
            const welcome = page.getByRole('region', { name: 'Welcome to Exceptionless' });
            await expect(welcome).toBeVisible();
            const dismissed = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
            await welcome.getByRole('button', { name: 'Close welcome' }).focus();
            await page.keyboard.press('Escape');
            await dismissed;
            await expect(welcome).toBeHidden();
        });

        await test.step('the shell tour renders on mobile and resumes on desktop with reduced motion', async () => {
            await page.setViewportSize({ height: 844, width: 390 });
            await startTourFromCommand(page, 'Explore Exceptionless');
            const tour = page.locator('.driver-popover');
            await expect(page.locator('[data-tour="app-navigation"]')).toBeVisible();
            await expect(tour.getByText('Your workspace navigation')).toBeVisible();

            await page.emulateMedia({ reducedMotion: 'reduce' });
            await page.setViewportSize({ height: 900, width: 1440 });
            const closeButton = tour.getByRole('button', { name: 'End guide' });
            await expect(closeButton).toHaveText('×');
            const closeBounds = await closeButton.boundingBox();
            const titleBounds = await tour.locator('.driver-popover-title').boundingBox();
            const descriptionBounds = await tour.locator('.driver-popover-description').boundingBox();
            const continueBounds = await tour.getByRole('button', { name: 'Continue' }).boundingBox();
            expect(closeBounds).not.toBeNull();
            expect(titleBounds).not.toBeNull();
            expect(descriptionBounds).not.toBeNull();
            expect(continueBounds?.height).toBe(32);
            expect(closeBounds?.height).toBe(32);
            expect(titleBounds!.x + titleBounds!.width).toBeLessThanOrEqual(closeBounds!.x);
            expect(closeBounds!.y + closeBounds!.height).toBeLessThanOrEqual(descriptionBounds!.y);
            await tour.getByRole('button', { name: 'Continue' }).click();
            await expect(tour.getByText('Use the command palette')).toBeVisible();
            await page.reload();
            await expect(tour.getByText('Use the command palette')).toBeVisible();

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
                ['Use the command palette', '[data-tour="command-search"]'],
                ['Find your saved views', '[data-tour="saved-view-navigation"]'],
                ['Ask Exie with context', '[data-tour="exie-trigger"]'],
                ['Find your next guide', '[data-tour="guided-tours-menu-item"]']
            ] as const) {
                await expect(tour.getByText(title)).toBeVisible();
                await expect(page.locator(target)).toBeVisible();
                if (title !== 'Find your next guide') {
                    await tour.getByRole('button', { name: 'Continue' }).click();
                }
            }

            const guidedTours = page.getByRole('menuitem', { exact: true, name: 'Guided Tours…' });
            await expect(guidedTours).toBeVisible();
            await expect(guidedTours).toHaveClass(/driver-active-element/);
            await expect(page.getByRole('menuitem', { exact: true, name: 'Help' })).toHaveAttribute('data-state', 'open');
            await expect(guidedTours).toBeInViewport();
            const completed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
            await tour.getByRole('button', { name: 'Browse guides' }).click();
            await completed;
            await expectProductTourSession(page, false);
            await expect(page.getByRole('dialog', { exact: true, name: 'Guided Tours' })).toBeVisible();
            await page.keyboard.press('Escape');
        });

        await test.step('an organization change clears an active checkpoint even when projects fail to load', async () => {
            await mockAssistantAccess(page);
            await page.reload();
            await startTourFromCommand(page, 'Meet Exie');
            await expectProductTourSession(page, true);
            const writesBeforeSwitch = progressWrites.length;
            const projectsRoute = `**/api/v2/organizations/${e2eSecondaryOrganization.organizationId}/projects*`;
            await page.route(projectsRoute, (route) => route.fulfill({ json: { title: 'Injected project lookup failure' }, status: 500 }));

            const identityTab = await page.context().newPage();
            await identityTab.goto('/next/stack');
            await identityTab.evaluate((organizationId) => {
                window.localStorage.setItem('organization', JSON.stringify(organizationId));
            }, e2eSecondaryOrganization.organizationId);
            await identityTab.close();
            await expectProductTourSession(page, false);
            expect(progressWrites).toHaveLength(writesBeforeSwitch);
            await page.getByRole('button', { name: 'Search Exceptionless' }).click();
            await page.getByRole('dialog').getByText('Guided Tours…', { exact: true }).click();
            const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
            await expect(catalog.getByRole('button', { exact: true, name: 'Restart Explore Exceptionless' })).toBeEnabled();
            await expect(catalog.getByRole('button', { exact: true, name: 'Start Configure a project' })).toBeDisabled();
            await expect(catalog.getByText('Projects could not be loaded. Try again shortly.', { exact: true })).toBeVisible();
            await page.keyboard.press('Escape');
            await page.unroute(projectsRoute);
            await page.reload();
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

test('project guide preserves the current SDK selection', async ({ e2eScenario, page }) => {
    // Arrange
    await page.route('**/api/v2/organizations/*/projects*', async (route) => {
        await route.fulfill({ json: [] });
    });
    await page.goto(`/next/project/${e2eScenario.projectId}/configure?type=dotnet-legacy-mvc`);
    await expect(page.locator('[data-tour="project-configure-platform"]')).toContainText('ASP.NET MVC');

    // Act
    await startTourFromCommand(page, 'Configure a project');

    // Assert
    await expect(page.getByRole('button', { exact: true, name: 'End guide' })).toBeVisible();
    await expect(page.locator('.driver-popover')).toHaveCount(0);
    await expect(page.locator('[data-tour="project-configure-platform"]')).toContainText('ASP.NET MVC');
    expect(new URL(page.url()).searchParams.get('type')).toBe('dotnet-legacy-mvc');
    expect(new URL(page.url()).searchParams.get('redirect')).toBe('true');
    expect(new URL(page.url()).pathname).toBe(`/next/project/${e2eScenario.projectId}/configure`);
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

        const projectProgressRoute = (url: URL) => url.pathname === '/api/v2/users/me/product-tours/project-configure';
        try {
            await page.locator('[data-tour="project-configure-platform"]').click();
            await page.getByRole('option', { name: 'Browser applications' }).click();
            await expect(page.getByText('Waiting for your first event')).toBeVisible();
            await expect(page.locator('.driver-popover')).toHaveCount(0);
            await expect(page.locator('.driver-overlay')).toHaveCount(0);
            await expect(page.locator('[data-tour="project-sdk-instructions"]')).toBeVisible();
            await expect(page.getByRole('button', { exact: true, name: 'End guide' })).toBeVisible();

            const instructionButtons = page.locator('[data-tour="project-sdk-instructions"]').getByRole('button');
            const reachedButtons = new Set<number>();
            await page.locator('[data-tour="project-configure-platform"]').focus();
            for (let tab = 0; tab < 40 && reachedButtons.size < (await instructionButtons.count()); tab++) {
                const focusedIndex = await instructionButtons.evaluateAll((buttons) => buttons.indexOf(document.activeElement as HTMLButtonElement));
                if (focusedIndex >= 0) {
                    reachedButtons.add(focusedIndex);
                }
                await page.keyboard.press('Tab');
            }
            expect(reachedButtons.size).toBe(await instructionButtons.count());

            let projectProgressRequests = 0;
            await page.route(projectProgressRoute, async (route) => {
                projectProgressRequests += 1;
                await route.fulfill({ json: { title: 'Injected progress failure' }, status: 500 });
            });
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
            await tour.getByRole('button', { name: 'Open View' }).click();
            await expect(page.locator('[data-tour="saved-view-save-as"]')).toHaveClass(/driver-active-element/);
            await tour.getByRole('button', { name: 'Save As…' }).click();

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
        await page.locator('.driver-popover').getByRole('button', { name: 'Open first error' }).click();
        const callout = page.locator('.driver-popover');
        await expect(callout.getByText('Understand the grouped issue')).toBeVisible();
        for (const title of ['Review the issue status', 'Inspect the occurrence', 'Begin with the overview', 'Compare every occurrence']) {
            await callout.getByRole('button', { name: 'Continue' }).click();
            await expect(callout.getByText(title)).toBeVisible();
        }

        const completed = page.waitForResponse(isSuccessfulTourProgress('event-investigate'));
        await callout.getByRole('button', { name: 'Finish guide' }).click();
        await completed;
        await expectProductTourSession(page, false);
        await page.reload();
        await expect(page.locator('.driver-popover')).toBeHidden();
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
            await tour.getByRole('button', { name: 'Open Exie' }).click();
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

test('privacy opt-out preserves completion and storage denial keeps guides usable', async ({ e2eScenario, page }, testInfo) => {
    // Arrange
    const activities: { action: string; step?: string }[] = [];
    const oversized = await page.request.post('/api/v2/users/me/product-tours/app-overview/activity', {
        data: JSON.stringify({ action: 'started', source: 'catalog', version: 1 }) + ' '.repeat(2048),
        headers: { Authorization: `Bearer ${e2eScenario.userToken}`, 'Content-Type': 'application/json' }
    });
    expect(oversized.status()).toBe(413);
    page.on('request', (request) => {
        if (request.method() === 'POST' && request.url().includes('/product-tours/app-overview/activity')) {
            activities.push(request.postDataJSON());
        }
    });
    await page.goto('/next/stack');
    await startTourFromCommand(page, 'Explore Exceptionless');
    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Your workspace navigation')).toBeVisible();
    await expect.poll(() => activities.filter((activity) => activity.step === 'navigation')).toHaveLength(1);
    await tour.getByRole('button', { name: 'Continue' }).click();
    await expect(tour.getByText('Use the command palette')).toBeVisible();
    await tour.getByRole('button', { name: 'Back' }).click();
    await expect(tour.getByText('Your workspace navigation')).toBeVisible();
    expect(activities.filter((activity) => activity.step === 'navigation')).toHaveLength(1);
    const dismissed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
    await tour.getByRole('button', { name: 'End guide' }).click();
    await dismissed;

    // Act
    await page.goto('/next/account/manage#guided-tour-privacy');
    const preference = page.getByRole('switch', { name: 'Help improve guided tours' });
    await expect(preference).toBeChecked();
    const saved = page.waitForResponse((response) => response.url().endsWith('/users/me/product-tour-analytics') && response.status() === 204);
    await preference.focus();
    await page.keyboard.press('Space');
    await saved;
    await expect(preference).not.toBeChecked();
    await page.screenshot({ path: testInfo.outputPath('guide-privacy.png') });
    const before = activities.length;
    await page.addInitScript(() =>
        Object.defineProperty(window, 'sessionStorage', {
            get() {
                throw new DOMException('Storage denied', 'SecurityError');
            }
        })
    );
    await page.goto('/next/stack');
    await startTourFromCommand(page, 'Explore Exceptionless');
    for (const title of ['Your workspace navigation', 'Use the command palette', 'Find your saved views']) {
        await expect(tour.getByText(title)).toBeVisible();
        await tour.getByRole('button', { name: 'Continue' }).click();
    }
    await expect(tour.getByText('Find your next guide')).toBeVisible();
    const completed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
    await tour.getByRole('button', { name: 'Browse guides' }).click();
    const response = await completed;

    // Assert
    expect(await response.json()).toMatchObject({ status: 1, version: 1 });
    expect(activities).toHaveLength(before);
    await expect(page.getByRole('dialog', { name: 'Guided Tours' })).toBeVisible();
    expect(e2eScenario.email).toContain('@exceptionless.test');
});

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
    await tour.getByRole('button', { name: /^(Continue|Restart|Start) / }).click();
}
