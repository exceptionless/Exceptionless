import type { Page, Request, Response } from '@playwright/test';

import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { createRepresentativeEvent } from '../support/synthetic-event';

test.use({ actionTimeout: 15_000, e2eUseInvitedUser: true });

test.describe('first-run welcome', () => {
    test.use({ e2eDismissProductTourWelcome: false });

    for (const [tourName, dismissLabel] of [
        ['app-welcome', 'Close welcome'],
        ['exie-announcement', 'Dismiss Exie announcement']
    ] as const) {
        test(`${tourName} stays dismissed when telemetry and session storage are unavailable`, async ({ e2eApi, e2eScenario, page }) => {
            // Arrange
            await page.route('**/api/v2/events', (route) => route.abort());
            if (tourName === 'exie-announcement') {
                await e2eApi.recordProductTour(e2eScenario.userToken, 'app-welcome');
            }
            await mockAssistantAccess(page);
            await page.goto('/next/stack');
            const dismiss = page.getByRole('button', { name: dismissLabel });
            await expect(dismiss).toBeVisible();

            // Act
            const persisted = page.waitForResponse(isSuccessfulTourProgress(tourName));
            await dismiss.click();
            expect(await (await persisted).json()).toMatchObject({ recorded_utc: expect.any(String) });
            await expect(dismiss).toBeHidden();
            await page.addInitScript(() =>
                Object.defineProperty(window, 'sessionStorage', {
                    get() {
                        throw new DOMException('Storage denied', 'SecurityError');
                    }
                })
            );
            const reloadedUser = page
                .waitForResponse((response) => new URL(response.url()).pathname === '/api/v2/users/me' && response.status() === 200)
                .then((response) => response.json());
            const reloadedProjects = page.waitForResponse(
                (response) => new URL(response.url()).pathname === `/api/v2/organizations/${e2eScenario.organizationId}/projects` && response.status() === 200
            );
            const [, currentUser] = await Promise.all([page.reload(), reloadedUser, reloadedProjects]);

            // Assert
            expect(currentUser).toMatchObject({
                product_tours: { [tourName.replaceAll('-', '_')]: expect.any(String) }
            });
            await expect(page.getByRole('button', { name: 'Search Exceptionless' })).toBeVisible();
            if (tourName === 'app-welcome') {
                // A different, unseen invitation remains eligible; saved outcomes do not hide unrelated guides.
                await expect(page.getByRole('button', { name: 'Dismiss Exie announcement' })).toBeVisible();
            }
            await expect(dismiss).toBeHidden();
        });
    }

    test('a manual guide does not compete with or accept the pending welcome', async ({ e2eScenario, page }) => {
        // Arrange
        const welcome = page.getByRole('region', { name: 'Welcome to Exceptionless' });
        await test.step(`show the pending welcome for ${e2eScenario.email}`, async () => {
            await page.goto('/next/stack/all');
            await expect(welcome).toBeVisible();
        });
        const invitationWrites: Request[] = [];
        page.on('request', (request) => {
            if (request.method() === 'PUT' && new URL(request.url()).pathname.endsWith('/product-tours/app-welcome/record')) {
                invitationWrites.push(request);
            }
        });

        // Act
        await startTourFromCommand(page, 'Explore Exceptionless');

        // Assert
        const guide = page.locator('.driver-popover');
        await expect(guide.getByText('Your workspace navigation')).toBeVisible();
        await expect(welcome).toBeHidden();
        await guide.getByRole('button', { name: 'End guide' }).click();
        await expect(guide).toBeHidden();
        await expect(welcome).toBeHidden();
        expect(invitationWrites).toEqual([]);
    });

    test('Browse Guides saves acknowledgment and opens the catalog', async ({ e2eScenario, page }, testInfo) => {
        // Arrange
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

        // Act
        const persisted = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
        await page.getByRole('region', { name: 'Welcome to Exceptionless' }).getByRole('button', { name: 'Browse guides' }).click();
        await persisted;

        // Assert
        const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
        await expect(catalog).toBeVisible();

        // Act
        await catalog.getByRole('button', { name: 'Close' }).click();
        await page.reload();

        // Assert
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

    test('a failed close is non-blocking and stays dismissed for the session', async ({ e2eScenario, page }) => {
        // Arrange
        const welcome = page.getByRole('region', { name: 'Welcome to Exceptionless' });
        await test.step(`show the welcome for ${e2eScenario.email}`, async () => {
            await page.goto('/next/stack');
            await expect(welcome).toBeVisible();
        });
        const progressRoute = '**/api/v2/users/me/product-tours/app-welcome/record';
        await page.route(progressRoute, (route) => route.fulfill({ json: { title: 'Injected progress failure' }, status: 500 }));

        // Act
        await welcome.getByRole('button', { name: 'Close welcome' }).click();

        // Assert
        await expect(page.getByText('We could not save your guided-tour preference. Please try again.')).toBeVisible();
        await expect(welcome).toBeHidden();
        await page.unroute(progressRoute);
        await page.reload();
        await expect(welcome).toBeHidden();
    });
});

test.describe('shell and identity checkpoints', () => {
    test.use({ e2eDismissProductTourWelcome: false });

    test('supports responsive guides and clears them on reload or identity changes', async ({ e2eApi, e2eScenario, e2eSecondaryOrganization, page }) => {
        // Arrange
        test.setTimeout(240_000);
        const progressWrites: string[] = [];
        page.on('request', (request) => {
            if (request.method() === 'PUT' && request.url().includes('/api/v2/users/me/product-tours/')) {
                progressWrites.push(new URL(request.url()).pathname);
            }
        });

        // Act & Assert: each step checks a responsive or identity transition.
        await test.step('closing the welcome persists dismissal', async () => {
            // Arrange
            await page.goto('/next/stack');
            const welcome = page.getByRole('region', { name: 'Welcome to Exceptionless' });
            await expect(welcome).toBeVisible();
            const dismissed = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
            // Act
            await welcome.getByRole('button', { name: 'Close welcome' }).focus();
            await page.keyboard.press('Escape');
            await dismissed;
            // Assert
            await expect(welcome).toBeHidden();
        });

        await test.step('the shell tour renders on mobile and resumes on desktop with reduced motion', async () => {
            // Arrange
            await page.setViewportSize({ height: 844, width: 390 });
            // Act
            await startTourFromCommand(page, 'Explore Exceptionless');
            const tour = page.locator('.driver-popover');
            // Assert
            await expect(page.locator('[data-tour="app-navigation"]')).toBeVisible();
            await expect(tour.getByText('Your workspace navigation')).toBeVisible();

            // Act
            await page.emulateMedia({ reducedMotion: 'reduce' });
            await page.setViewportSize({ height: 900, width: 1440 });
            const closeButton = tour.getByRole('button', { name: 'End guide' });
            // Assert
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
            // Act
            await tour.getByRole('button', { name: 'Continue' }).click();
            // Assert
            await expect(tour.getByText('Use the command palette')).toBeVisible();
            // Act
            await page.reload();
            // Assert
            await expect(tour).toBeHidden();
            // Act
            await startTourFromCommand(page, 'Explore Exceptionless');
            // Assert
            await expect(tour.getByText('Your workspace navigation')).toBeVisible();
            const writesBeforeDismissal = progressWrites.length;
            // Act
            await tour.getByRole('button', { name: 'End guide' }).click();
            // Assert
            expect(progressWrites).toHaveLength(writesBeforeDismissal);
            await expectActiveProductTour(page, false);
        });

        await test.step('every shell target remains visible on mobile', async () => {
            // Arrange
            await mockAssistantAccess(page);
            await page.reload();
            await page.setViewportSize({ height: 844, width: 390 });
            // Act
            await startTourFromCommand(page, 'Explore Exceptionless');
            const tour = page.locator('.driver-popover');

            // Assert
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
                    // Act
                    await tour.getByRole('button', { name: 'Continue' }).click();
                }
            }

            // Assert
            const guidedTours = page.getByRole('menuitem', { exact: true, name: 'Guided Tours…' });
            await expect(guidedTours).toBeVisible();
            await expect(guidedTours).toHaveClass(/driver-active-element/);
            await expect(page.getByRole('menuitem', { exact: true, name: 'Help' })).toHaveAttribute('data-state', 'open');
            await expect(guidedTours).toBeInViewport();
            const completed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
            // Act
            await tour.getByRole('button', { name: 'Browse guides' }).click();
            await completed;
            // Assert
            await expectActiveProductTour(page, false);
            await expect(page.getByRole('dialog', { exact: true, name: 'Guided Tours' })).toBeVisible();
            await page.keyboard.press('Escape');
        });

        await test.step('an organization change clears an active checkpoint even when projects fail to load', async () => {
            // Arrange
            await mockAssistantAccess(page);
            await page.reload();
            await startTourFromCommand(page, 'Meet Exie');
            await expectActiveProductTour(page, true);
            const writesBeforeSwitch = progressWrites.length;
            const projectsRoute = `**/api/v2/organizations/${e2eSecondaryOrganization.organizationId}/projects*`;
            const projectLookup = Promise.withResolvers<void>();
            await page.route(projectsRoute, async (route) => {
                await projectLookup.promise;
                await route.fulfill({ json: { title: 'Injected project lookup failure' }, status: 500 });
            });

            // Act
            const identityTab = await page.context().newPage();
            await identityTab.goto('/next/stack');
            await identityTab.evaluate((organizationId) => {
                window.localStorage.setItem('organization', JSON.stringify(organizationId));
            }, e2eSecondaryOrganization.organizationId);
            await identityTab.close();
            // Assert
            await expectActiveProductTour(page, false);
            expect(progressWrites).toHaveLength(writesBeforeSwitch);
            // Act
            await page.getByRole('button', { name: 'Search Exceptionless' }).click();
            await page.getByRole('dialog').getByText('Guided Tours…', { exact: true }).click();
            const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
            try {
                // Assert
                // Shell guides do not depend on the pending project lookup.
                await expect(catalog.getByRole('button', { exact: true, name: 'Restart Explore Exceptionless' })).toBeEnabled();
            } finally {
                projectLookup.resolve();
            }
            await expect(catalog.getByRole('button', { exact: true, name: 'Start Configure a project' })).toBeDisabled();
            await expect(catalog.getByText('Projects could not be loaded. Try again shortly.', { exact: true })).toBeVisible();
            await page.keyboard.press('Escape');
            await page.unroute(projectsRoute);
            await page.reload();
        });

        await test.step('logout clears an active checkpoint without recording progress', async () => {
            // Arrange
            await page.setViewportSize({ height: 900, width: 1440 });
            await startTourFromCommand(page, 'Meet Exie');
            await expectActiveProductTour(page, true);
            const writesBeforeLogout = progressWrites.length;

            // Act
            await page.getByRole('button', { name: new RegExp(e2eScenario.userName) }).dispatchEvent('click');
            await page.getByRole('menuitem', { name: 'Log Out' }).dispatchEvent('click');
            // Assert
            await expect(page).toHaveURL(/\/next\/login/);
            await expectActiveProductTour(page, false);
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

test('a saved-view guide allows submitting the form before finishing its steps', async ({ e2eScenario, page }) => {
    // Arrange
    await page.goto('/next/event');
    await startTourFromCommand(page, 'Create a saved view');
    const guide = page.locator('.driver-popover');
    await guide.getByRole('button', { name: 'Open View' }).click();
    await guide.getByRole('button', { name: 'Save As…' }).click();
    const name = page.getByLabel('Name', { exact: true });
    await name.fill(`Early Save ${e2eScenario.run}`);
    const completed = page.waitForResponse(isSuccessfulTourProgress('saved-view-create'));

    // Act
    await name.press('Enter');

    // Assert
    await completed;
    await expectActiveProductTour(page, false);
    await expect(page.getByText('Your saved view is ready', { exact: true })).toBeVisible();
});

test('domain workflows advance only on real success', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange: the invited-user fixture supplies the organization and project.
    test.setTimeout(300_000);

    // Act & Assert: each workflow below exercises and verifies its own transitions.
    await test.step('project configuration advances after setup and the first event', async () => {
        // Arrange
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

        const projectProgressRoute = (url: URL) => url.pathname === '/api/v2/users/me/product-tours/project-configure/record';
        try {
            // Act
            await page.locator('[data-tour="project-configure-platform"]').click();
            await page.getByRole('option', { name: 'Browser applications' }).click();
            // Assert
            await expect(page.getByText('Waiting for your first event')).toBeVisible();
            await expect(page.locator('.driver-popover')).toHaveCount(0);
            await expect(page.locator('.driver-overlay')).toHaveCount(0);
            await expect(page.locator('[data-tour="project-sdk-instructions"]')).toBeVisible();
            await expect(page.getByRole('button', { exact: true, name: 'End guide' })).toBeVisible();

            // Act
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
            // Assert
            expect(reachedButtons.size).toBe(await instructionButtons.count());

            // Arrange
            let projectProgressRequests = 0;
            await page.route(projectProgressRoute, async (route) => {
                projectProgressRequests += 1;
                await route.fulfill({ json: { title: 'Injected progress failure' }, status: 500 });
            });
            const token = await e2eApi.getProjectDefaultToken(e2eScenario.userToken, projectId!);
            // Act
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
            // Assert
            await expect(page).toHaveURL(/\/next\/event/);
            await expectActiveProductTour(page, false);
            await expect.poll(() => projectProgressRequests).toBe(1);
            await expect.poll(async () => (await e2eApi.getProject(e2eScenario.userToken, projectId!))?.is_configured).toBe(true);

            // Act
            await page.unroute(projectProgressRoute);
            await page.goto(`/next/project/${projectId}/configure`);
            // Assert
            await expectActiveProductTour(page, false);
            expect(projectProgressRequests).toBe(1);
        } finally {
            await page.unroute(projectProgressRoute);
            if (createdProject) {
                await e2eApi.deleteProject(e2eScenario.userToken, projectId!);
                await e2eApi.waitForProjectDeleted(e2eScenario.userToken, projectId!);
            }
        }
    });

    await test.step('saved-view completion closes without blocking when persistence fails', async () => {
        // Arrange
        let createRequests = 0;
        let progressRequests = 0;
        const countSavedViewCreation = (request: Request) => {
            const path = new URL(request.url()).pathname;
            if (request.method() === 'POST' && /^\/api\/v2\/organizations\/[^/]+\/saved-views$/.test(path)) {
                createRequests += 1;
            }
        };
        const progressRoute = (url: URL) => url.pathname === '/api/v2/users/me/product-tours/saved-view-create/record';
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
            // Act
            await startTourFromCommand(page, 'Create a saved view');
            // Assert
            await expectActiveProductTour(page, true);
            const tour = page.locator('.driver-popover');
            // Act
            await tour.getByRole('button', { name: 'Open View' }).click();
            // Assert
            await expect(page.locator('[data-tour="saved-view-save-as"]')).toHaveClass(/driver-active-element/);
            // Act
            await tour.getByRole('button', { name: 'Save As…' }).click();

            await page.getByLabel('Name', { exact: true }).fill(`Tour View ${e2eScenario.run}`);
            await page.getByRole('button', { name: 'Continue' }).click();
            await page.getByRole('button', { name: 'Continue' }).click();
            await page.getByRole('button', { exact: true, name: 'Save' }).click();
            // Assert
            await expect(page.getByText('Your saved view is ready', { exact: true })).toBeVisible();
            await expect(page.locator('.driver-popover')).toHaveCount(0);
            expect(createRequests).toBe(1);
            expect(progressRequests).toBe(1);

            // Act
            await page.reload();
            // Assert
            await expect(page.getByRole('button', { name: 'Retry guide completion' })).toHaveCount(0);
            await expect.poll(() => createRequests).toBe(1);
            await expectActiveProductTour(page, false);
            expect(progressRequests).toBe(1);
        } finally {
            page.off('request', countSavedViewCreation);
            await page.unroute(progressRoute);
        }
    });

    await test.step('investigation advances when a real error opens', async () => {
        // Arrange
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: e2eScenario.message,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: e2eScenario.referenceId
        });
        await page.goto('/next/event?time=all&type=error');
        await expect(page.getByText(e2eScenario.message).first()).toBeVisible({ timeout: 30_000 });
        // Act
        await startTourFromCommand(page, 'Investigate an error');
        await page.locator('.driver-popover').getByRole('button', { name: 'Continue' }).click();
        await page.locator('.driver-popover').getByRole('button', { name: 'Open first error' }).click();
        const callout = page.locator('.driver-popover');
        // Assert
        await expect(callout.getByText('Understand the grouped issue')).toBeVisible();
        for (const title of ['Review the issue status', 'Inspect the occurrence', 'Begin with the overview', 'Compare every occurrence']) {
            // Act
            await callout.getByRole('button', { name: 'Continue' }).click();
            // Assert
            await expect(callout.getByText(title)).toBeVisible();
        }

        const completed = page.waitForResponse(isSuccessfulTourProgress('event-investigate'));
        // Act
        await callout.getByRole('button', { name: 'Finish guide' }).click();
        await completed;
        // Assert
        await expectActiveProductTour(page, false);
        // Act
        await page.reload();
        // Assert
        await expect(page.locator('.driver-popover')).toBeHidden();
    });

    await test.step('Exie opens context without provider submission', async () => {
        // Arrange
        await mockAssistantAccess(page);
        let chatRequests = 0;
        const countChatRequest = (request: Request) => {
            if (new URL(request.url()).pathname === '/api/v2/assistant/chat') {
                chatRequests += 1;
            }
        };
        page.on('request', countChatRequest);

        try {
            await page.goto('/next/stack');
            // Act
            await startTourFromCommand(page, 'Meet Exie');
            const tour = page.locator('.driver-popover');
            await tour.getByRole('button', { name: 'Open Exie' }).click();
            // Assert
            await expect(tour.getByText('You control every request')).toBeVisible();
            expect(chatRequests).toBe(0);
        } finally {
            page.off('request', countChatRequest);
        }
    });
});

async function expectActiveProductTour(page: Page, present: boolean): Promise<void> {
    const guide = page.getByRole('button', { exact: true, name: 'End guide' });
    if (present) {
        await expect(guide).toBeVisible();
    } else {
        await expect(guide).toBeHidden();
    }
}

test('completion survives unavailable telemetry and session storage', async ({ e2eScenario, page }) => {
    // Arrange
    await page.route('**/api/v2/events', (route) => route.abort());
    const tour = page.locator('.driver-popover');
    await page.addInitScript(() =>
        Object.defineProperty(window, 'sessionStorage', {
            get() {
                throw new DOMException('Storage denied', 'SecurityError');
            }
        })
    );
    await page.goto('/next/stack');
    // Act
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
    expect(await response.json()).toMatchObject({ recorded_utc: expect.any(String) });
    await expect(page.getByRole('dialog', { name: 'Guided Tours' })).toBeVisible();
    expect(e2eScenario.email).toContain('@exceptionless.test');
});

function isSuccessfulTourProgress(tourName: string) {
    return (response: Response): boolean => {
        const path = new URL(response.url()).pathname;
        return response.request().method() === 'PUT' && path === `/api/v2/users/me/product-tours/${tourName}/record` && response.status() === 200;
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
