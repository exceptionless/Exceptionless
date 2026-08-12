import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';

test.use({ e2eUseGeneratedUser: true });

test('Explore the new UI is replayable from command search', async ({ e2eScenario: _e2eScenario, page }, testInfo) => {
    void _e2eScenario;
    await page.setViewportSize({ height: 900, width: 1440 });
    await page.goto('/next/stack');

    await startTourFromCommand(page, 'Explore the new UI');
    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Your workspace navigation')).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('new-ui-overview-desktop.png') });
    await tour.getByRole('button', { name: 'Close' }).click();
    await expect(tour).toBeHidden();

    await startTourFromCommand(page, 'Explore the new UI');
    await expect(tour.getByText('Your workspace navigation')).toBeVisible();
    await tour.getByRole('button', { name: 'Close' }).click();
});

test('Explore the new UI opens its navigation target on mobile', async ({ e2eScenario: _e2eScenario, page }, testInfo) => {
    void _e2eScenario;
    await page.setViewportSize({ height: 844, width: 390 });
    await page.goto('/next/stack');

    await startTourFromCommand(page, 'Explore the new UI');
    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Your workspace navigation')).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('new-ui-overview-mobile.png') });
    await tour.getByRole('button', { name: 'Next' }).click();
    await expect(tour.getByText('Reuse configured views')).toBeVisible();
    await tour.getByRole('button', { name: 'Next' }).click();
    await expect(tour.getByText('Help is always nearby')).toBeVisible();
    await tour.getByRole('button', { name: 'Next' }).click();
    await expect(tour.getByText('Find anything quickly')).toBeVisible();
    await expect(page.locator('[data-tour="mobile-navigation-trigger"]')).toBeVisible();
    await tour.getByRole('button', { name: 'Close' }).click();
});

test('Configure a project resumes through its first event', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    await page.setViewportSize({ height: 900, width: 1440 });
    await page.goto('/next/stack');

    let createdProjectId: string | undefined;
    await startTourFromCommand(page, 'Configure a project');
    if (await page.getByRole('alertdialog', { name: 'Create another project?' }).isVisible()) {
        await page.getByRole('button', { name: 'Create Project' }).click();
        await expect(page.getByRole('heading', { name: 'Add Project' })).toBeVisible();
        await page.getByLabel('Project Name', { exact: true }).fill(`Tour Project ${e2eScenario.run}`);
        await expect(page.locator('.driver-popover').getByText('Name your project', { exact: true })).toBeVisible();
        await page.locator('.driver-popover').getByRole('button', { name: 'Next' }).click();
        await expect(page.locator('.driver-popover').getByText('Continue to configuration')).toBeVisible();
        await page.locator('.driver-popover').getByRole('button', { name: 'Continue' }).click();
        await page.waitForURL(/\/next\/project\/[^/]+\/configure\?redirect=true/);
        createdProjectId = page.url().match(/\/project\/([^/]+)\/configure/)?.[1];
    }

    await page.waitForURL(/\/next\/project\/[^/]+\/configure\?redirect=true/);
    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Choose your SDK')).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('configure-project-platform.png') });

    await page.locator('[data-tour="project-configure-platform"]').click();
    await page.getByRole('option', { name: 'Browser applications' }).click();
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(tour.getByText('Use the project token')).toBeVisible();
    await page.getByRole('button', { name: 'Next' }).click();
    await expect(page.getByText('Connect your application', { exact: true })).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('configure-project-inline.png') });
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByText('Waiting for your first event')).toBeVisible();

    try {
        const projectId = createdProjectId ?? e2eScenario.projectId;
        const projectToken = createdProjectId ? (await e2eApi.getProjectDefaultToken(e2eScenario.userToken, createdProjectId)).id : e2eScenario.projectToken;
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: e2eScenario.message,
            projectId,
            projectToken,
            referenceId: e2eScenario.referenceId
        });
        await expect(page).toHaveURL(/\/next\/event/);
        await expect(page.getByText('First event received. Opening Events...')).toBeHidden();
    } finally {
        if (createdProjectId) {
            await e2eApi.deleteProject(e2eScenario.userToken, createdProjectId);
            await e2eApi.waitForProjectDeleted(e2eScenario.userToken, createdProjectId);
        }
    }
});

test('Create a saved view retains a private hydrated view', async ({ e2eScenario, page }, testInfo) => {
    await page.setViewportSize({ height: 900, width: 1440 });
    await page.goto('/next/event');

    await startTourFromCommand(page, 'Create a saved view');
    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Open View settings')).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('saved-view-open.png') });
    await tour.getByRole('button', { name: 'Continue' }).click();
    await expect(tour.getByText('Configure what the view remembers')).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('saved-view-settings.png') });
    await tour.getByRole('button', { name: 'Next' }).click();
    await expect(tour.getByText('Create a new view')).toBeVisible();
    await tour.getByRole('button', { name: 'Continue' }).click();

    const viewName = `Tour View ${e2eScenario.run}`;
    await expect(page.getByText('Review and name your view')).toBeVisible();
    await page.getByLabel('Name', { exact: true }).fill(viewName);
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByText('Keep it private')).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('saved-view-private.png') });
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByText('Create the saved view')).toBeVisible();
    await page.getByRole('button', { exact: true, name: 'Save' }).click();

    await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
    await expect(page).toHaveURL(/\/next\/event\/[^/]+/);
    await expect(page.getByText('Create the saved view')).toBeHidden();
});

test('Investigate an error resumes after navigation and advances only after an error opens', async ({ e2eApi, e2eScenario, page }, testInfo) => {
    const event = await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
        message: e2eScenario.message,
        projectId: e2eScenario.projectId,
        projectToken: e2eScenario.projectToken,
        referenceId: e2eScenario.referenceId
    });

    await page.setViewportSize({ height: 900, width: 1440 });
    await page.goto('/next/event?time=all');
    await expect(page.getByText(e2eScenario.message)).toBeVisible({ timeout: 30_000 });
    await startTourFromCommand(page, 'Investigate an error');
    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Open a real error')).toBeVisible();
    await expect(page).toHaveURL(/\/next\/event\?time=all&type=error/);
    await tour.getByRole('button', { name: 'Close' }).click();

    await page.goto('/next/stack');
    await startTourFromCommand(page, 'Investigate an error');
    const navigationConfirmation = page.getByRole('alertdialog', { name: 'Open Errors?' });
    await expect(navigationConfirmation).toBeVisible();
    await navigationConfirmation.getByRole('button', { name: 'Open Errors' }).click();
    await expect(tour.getByText('Open a real error')).toBeVisible();
    await expect(page).toHaveURL(/\/next\/event\?time=all&type=error/);
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('investigate-error-list.png') });
    await page.locator('tr').filter({ hasText: e2eScenario.message }).first().click();
    const investigationCallout = page.locator('[data-product-tour-inline="investigate-error"]');
    await expect(investigationCallout.getByText('Investigate the evidence')).toBeVisible({ timeout: 30_000 });
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('investigate-error-details.png') });
    await investigationCallout.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByText('Investigate the evidence')).toBeHidden();

    await page.goto(`/next/event/${event.id}`);
    await expect(page.locator('[data-tour="event-details"]')).toBeVisible({ timeout: 30_000 });
    await startTourFromCommand(page, 'Investigate an error');
    await expect(investigationCallout.getByText('Investigate the evidence')).toBeVisible();
    await investigationCallout.getByRole('button', { name: 'End guide' }).click();
    expect(event.type).toBe('error');

    const usageReferenceId = `pw-e2e-tour-usage-${e2eScenario.run}`.slice(0, 100);
    await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, {
        data: { e2e_reference: usageReferenceId },
        message: `Feature usage ${e2eScenario.run}`,
        reference_id: usageReferenceId,
        source: 'playwright-e2e',
        type: 'usage'
    });
    const usageEvent = await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, usageReferenceId);
    await page.goto(`/next/event/${usageEvent.id}`);
    await expect(page.locator('[data-tour="event-details"][data-event-type="usage"]')).toBeVisible({ timeout: 30_000 });

    const usageUrl = page.url();
    await startTourFromCommand(page, 'Investigate an error');
    const nonErrorConfirmation = page.getByRole('alertdialog', { name: 'Open Errors?' });
    await expect(nonErrorConfirmation).toBeVisible();
    await nonErrorConfirmation.getByRole('button', { name: 'Cancel' }).click();
    await expect(nonErrorConfirmation).toBeHidden();
    expect(page.url()).toBe(usageUrl);
    await expect(investigationCallout).toBeHidden();
});

test('Exie announcement can be dismissed without hiding the replayable guide', async ({ e2eScenario: _e2eScenario, page }, testInfo) => {
    void _e2eScenario;
    await page.route('**/api/v2/assistant/access**', async (route) => {
        await route.fulfill({
            contentType: 'application/json',
            json: { enabled: true, has_access: true, message: null, upgrade_required: false }
        });
    });

    await page.setViewportSize({ height: 900, width: 1440 });
    await page.goto('/next/stack');

    const announcement = page.locator('[data-product-tour-announcement="exie"]');
    await expect(announcement).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('meet-exie-announcement.png') });
    const dismissed = page.waitForResponse(
        (response) => response.url().includes('/product-tours/exie-announcement') && response.request().method() === 'PUT' && response.status() === 200
    );
    await announcement.getByRole('button', { exact: true, name: 'Dismiss' }).click();
    await dismissed;
    await page.reload();
    await expect(announcement).toBeHidden();

    await startTourFromCommand(page, 'Meet Exie');
    await expect(page.locator('.driver-popover').getByText('Open Exie', { exact: true })).toBeVisible();
});

test('Meet Exie opens contextual UI without sending a provider request', async ({ e2eScenario: _e2eScenario, page }, testInfo) => {
    void _e2eScenario;
    let chatRequests = 0;
    await page.route('**/api/v2/assistant/access**', async (route) => {
        await route.fulfill({
            contentType: 'application/json',
            json: { enabled: true, has_access: true, message: null, upgrade_required: false }
        });
    });
    await page.route('**/api/v2/assistant/chat**', async (route) => {
        chatRequests += 1;
        await route.abort();
    });

    await page.setViewportSize({ height: 900, width: 1440 });
    await page.goto('/next/stack');
    await startTourFromCommand(page, 'Meet Exie');

    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Open Exie', { exact: true })).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('meet-exie-trigger.png') });
    await tour.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByRole('dialog', { name: 'Exie' })).toBeVisible();
    await expect(tour.getByText('You control every request')).toBeVisible();
    await page.screenshot({ fullPage: true, path: testInfo.outputPath('meet-exie-panel.png') });
    expect(chatRequests).toBe(0);

    await tour.getByRole('button', { name: 'Next' }).click();
    expect(chatRequests).toBe(0);
});

async function startTourFromCommand(page: import('@playwright/test').Page, title: string): Promise<void> {
    await page.getByRole('button', { name: 'Search Exceptionless' }).click();
    await page.getByRole('dialog').getByText(title, { exact: true }).click();
}
