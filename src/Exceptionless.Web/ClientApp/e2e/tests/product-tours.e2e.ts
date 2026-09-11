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
        await expect(guide.getByText('Spot repeated problems')).toBeVisible();
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

    test('a failed close is non-blocking and can be retried after reload', async ({ e2eScenario, page }) => {
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

        // An unsaved preference can be retried when the user returns.
        await expect(welcome).toBeVisible();
        const persisted = page.waitForResponse(isSuccessfulTourProgress('app-welcome'));
        await welcome.getByRole('button', { name: 'Close welcome' }).click();
        await persisted;
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
            await expect(tour.getByText('Spot repeated problems')).toBeVisible();

            // Act
            await page.emulateMedia({ reducedMotion: 'reduce' });
            await page.setViewportSize({ height: 900, width: 1440 });
            const closeButton = tour.getByRole('button', { name: 'End guide' });
            // Assert
            await expect(closeButton).toHaveText('×');
            const closeBounds = await closeButton.boundingBox();
            const titleBounds = await tour.locator('.driver-popover-title').evaluate((element) => {
                const range = document.createRange();
                range.selectNodeContents(element);
                return range.getBoundingClientRect().toJSON();
            });
            const tourBounds = await tour.boundingBox();
            const descriptionBounds = await tour.locator('.driver-popover-description').boundingBox();
            const continueBounds = await tour.getByRole('button', { name: 'Next' }).boundingBox();
            expect(closeBounds).not.toBeNull();
            expect(titleBounds).not.toBeNull();
            expect(descriptionBounds).not.toBeNull();
            expect(continueBounds?.height).toBeGreaterThanOrEqual(32);
            expect(closeBounds?.height).toBe(32);
            expect(closeBounds!.x + closeBounds!.width).toBeCloseTo(tourBounds!.x + tourBounds!.width - 5, 0);
            expect(closeBounds!.y).toBeCloseTo(tourBounds!.y + 5, 0);
            expect(titleBounds!.x + titleBounds!.width).toBeLessThanOrEqual(closeBounds!.x);
            expect(closeBounds!.y + closeBounds!.height).toBeLessThanOrEqual(descriptionBounds!.y);
            // Act
            await tour.getByRole('button', { name: 'Next' }).click();
            // Assert
            await expect(tour.getByText('See each report')).toBeVisible();
            // Act
            await page.reload();
            // Assert
            await expect(tour).toBeHidden();
            // Act
            await startTourFromCommand(page, 'Explore Exceptionless');
            // Assert
            await expect(tour.getByText('Spot repeated problems')).toBeVisible();
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
                ['Spot repeated problems', '[data-tour="navigation-stacks"]'],
                ['See each report', '[data-tour="navigation-events"]'],
                ['Narrow your results', '[data-tour="event-filters"]'],
                ['Keep a useful view', '[data-tour="saved-view-trigger"]'],
                ['Get help from Exie', '[data-tour="exie-trigger"]'],
                ['Search and take action', '[data-tour="command-search"]']
            ] as const) {
                await expect(tour.getByText(title)).toBeVisible();
                await expect(page.locator(target)).toBeVisible();
                if (title !== 'Search and take action') {
                    // Act
                    await tour.getByRole('button', { name: 'Next' }).click();
                }
            }

            // Assert
            await expectCalloutBesideTarget(page);
            const completed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
            // Act
            await tour.getByRole('button', { name: 'Done' }).click();
            await completed;
            // Assert
            await expectActiveProductTour(page, false);
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
            await page.getByRole('dialog').getByRole('option', { exact: true, name: 'Guided Tours' }).click();
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
            await expectCalloutBesideTarget(page);
            // Act
            await tour.getByRole('button', { name: 'Save As…' }).click();

            await page.getByLabel('Name', { exact: true }).fill(`Tour View ${e2eScenario.run}`);
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
        await page.locator('.driver-popover').getByRole('button', { name: 'Open error' }).click();
        const callout = page.locator('.driver-popover');
        // Assert
        await expect(callout.getByText('See the impact')).toBeVisible();
        for (const title of ['Read what happened', 'See related reports']) {
            // Act
            await callout.getByRole('button', { name: 'Next' }).click();
            // Assert
            await expect(callout.getByText(title)).toBeVisible();
        }

        const completed = page.waitForResponse(isSuccessfulTourProgress('event-investigate'));
        // Act
        await expectCalloutBesideTarget(page);
        await page.locator('[data-tour="stack-events"]').click();
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
            await expect(tour.getByText('Ask your first question')).toBeVisible();
            expect(chatRequests).toBe(0);
        } finally {
            page.off('request', countChatRequest);
        }
    });
});

test('the error guide keeps the start of a wide report visible on mobile', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange
    await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
        message: e2eScenario.message,
        projectId: e2eScenario.projectId,
        projectToken: e2eScenario.projectToken,
        referenceId: e2eScenario.referenceId
    });
    await page.setViewportSize({ height: 844, width: 390 });
    await page.goto('/next/event?time=all&type=error');
    await expect(page.getByText(e2eScenario.message).first()).toBeVisible();

    // Act
    await startTourFromCommand(page, 'Investigate an error');
    await expect(page.locator('.driver-popover-title')).toHaveText('Take a closer look');

    // Assert: the row may extend past the right edge, but its report name must stay visible.
    const row = page.locator('.driver-active-element');
    await expect.poll(async () => (await row.boundingBox())?.x ?? -1).toBeGreaterThanOrEqual(0);
    await page.locator('.driver-popover').getByRole('button', { name: 'Open error' }).click();
    await expect(page.locator('.driver-popover-title')).toHaveText('See the impact');
    await expectCalloutBesideTarget(page);
});

test('overview arrows stay beside each control on desktop and mobile', async ({ e2eScenario, page }) => {
    // Arrange
    await mockAssistantAccess(page);
    await page.goto('/next/stack');
    expect(e2eScenario.email).toContain('@exceptionless.test');
    const titles = ['Spot repeated problems', 'See each report', 'Narrow your results', 'Keep a useful view', 'Get help from Exie', 'Search and take action'];

    for (const viewport of [
        { height: 900, width: 1440 },
        { height: 844, width: 390 }
    ]) {
        await page.setViewportSize(viewport);
        await page.emulateMedia({ reducedMotion: 'no-preference' });
        await startTourFromCommand(page, 'Explore Exceptionless');
        const tour = page.locator('.driver-popover');

        for (const [index, title] of titles.entries()) {
            // Assert: visibility alone misses detached cards and arrows.
            await expect(tour.getByText(title, { exact: true })).toBeVisible();
            await expect(tour.getByText(`Step ${index + 1} of 6`, { exact: true })).toBeVisible();
            await expectCalloutBesideTarget(page);

            // Act
            await tour.getByRole('button', { exact: true, name: index === titles.length - 1 ? 'Done' : 'Next' }).click();
        }
        await expectActiveProductTour(page, false);
    }

    // Act & Assert: leaving by keyboard remains available with reduced motion.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await startTourFromCommand(page, 'Explore Exceptionless');
    await expectCalloutBesideTarget(page);
    await page.keyboard.press('Escape');
    await expectActiveProductTour(page, false);
});

async function expectActiveProductTour(page: Page, present: boolean): Promise<void> {
    const guide = page.getByRole('button', { exact: true, name: 'End guide' });
    if (present) {
        await expect(guide).toBeVisible();
    } else {
        await expect(guide).toBeHidden();
    }
}

async function expectCalloutBesideTarget(page: Page): Promise<void> {
    await expect
        .poll(() =>
            page.evaluate(() => {
                const target = document.querySelector('.driver-active-element');
                const popover = document.querySelector<HTMLElement>('.driver-popover');
                const arrow = popover?.querySelector<HTMLElement>('.driver-popover-arrow');
                if (!target || !popover || !arrow) {
                    return false;
                }
                const targetBounds = target.getBoundingClientRect();
                const bounds = popover.getBoundingClientRect();
                const arrowBounds = arrow.getBoundingClientRect();
                const gap = Math.max(
                    targetBounds.left - bounds.right,
                    bounds.left - targetBounds.right,
                    targetBounds.top - bounds.bottom,
                    bounds.top - targetBounds.bottom
                );
                const verticalArrow = arrow.classList.contains('driver-popover-arrow-side-top') || arrow.classList.contains('driver-popover-arrow-side-bottom');
                const arrowCenter = verticalArrow ? arrowBounds.left + arrowBounds.width / 2 : arrowBounds.top + arrowBounds.height / 2;
                const targetStart = verticalArrow ? targetBounds.left : targetBounds.top;
                const targetEnd = verticalArrow ? targetBounds.right : targetBounds.bottom;
                return (
                    bounds.left >= 0 &&
                    bounds.top >= 0 &&
                    bounds.right <= innerWidth &&
                    bounds.bottom <= innerHeight &&
                    targetBounds.left >= 0 &&
                    targetBounds.top >= 0 &&
                    targetBounds.right <= innerWidth &&
                    targetBounds.bottom <= innerHeight &&
                    gap >= 0 &&
                    gap <= 24 &&
                    getComputedStyle(arrow).display !== 'none' &&
                    arrowCenter >= targetStart - 8 &&
                    arrowCenter <= targetEnd + 8
                );
            })
        )
        .toBe(true);
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
    await mockAssistantAccess(page);
    await page.goto('/next/stack');
    // Act
    await startTourFromCommand(page, 'Explore Exceptionless');
    for (const title of ['Spot repeated problems', 'See each report', 'Narrow your results', 'Keep a useful view', 'Get help from Exie']) {
        await expect(tour.getByText(title)).toBeVisible();
        await tour.getByRole('button', { name: 'Next' }).click();
    }
    await expect(tour.getByText('Search and take action')).toBeVisible();
    const completed = page.waitForResponse(isSuccessfulTourProgress('app-overview'));
    await tour.getByRole('button', { name: 'Done' }).click();
    const response = await completed;

    // Assert
    expect(await response.json()).toMatchObject({ recorded_utc: expect.any(String) });
    await expectActiveProductTour(page, false);
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
    await page.getByRole('dialog').getByRole('option', { exact: true, name: 'Guided Tours' }).click();
    const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
    const tour = catalog.getByRole('region', { name: title });
    await tour.getByRole('button', { name: /^(Continue|Restart|Start) / }).click();
}
