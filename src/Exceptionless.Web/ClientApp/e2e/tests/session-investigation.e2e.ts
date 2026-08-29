import { createReferenceId, expect, test } from '../fixtures/e2e-test';
import { getVisibleRow, getVisibleText } from '../support/page-helpers';
import { createRepresentativeEvent, createSessionEvent } from '../support/synthetic-event';

test('operator can find and inspect a user session', async ({ e2eApi, e2eScenario, page, request }) => {
    const sessionId = createReferenceId(e2eScenario.run, '-session');
    const eventReferenceId = createReferenceId(e2eScenario.run, '-session-error');
    const identity = `session-${e2eScenario.run}@exceptionless.test`;
    const name = `Session User ${e2eScenario.run}`;
    const longMessage = Array.from({ length: 42 }, (_, index) => `session-processing-failure-${String(index + 1).padStart(2, '0')}`).join(' ');
    let relatedEventId = '';
    let relatedStackId = '';

    await test.step('seed a representative session', async () => {
        const savedViewResponse = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
            data: {
                columns: {
                    date: { position: 1, visible: true },
                    summary: { position: 0, visible: true, wrap: true }
                },
                name: 'All',
                organization_id: e2eScenario.organizationId,
                slug: 'all',
                view_type: 'events'
            },
            headers: { Authorization: `Bearer ${e2eScenario.userToken}` }
        });
        expect(savedViewResponse.status(), await savedViewResponse.text()).toBe(201);

        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, createSessionEvent({ identity, name, sessionId }));
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, sessionId);

        const relatedEvent = createRepresentativeEvent({
            appUrl: e2eApi.environment.appUrl,
            message: longMessage,
            referenceId: eventReferenceId,
            runId: e2eApi.environment.runId
        });
        relatedEvent.data = {
            ...(relatedEvent.data as Record<string, unknown>),
            '@environment': {
                ip_address: '10.42.0.25',
                machine_name: 'session-worker-07',
                process_name: 'Exceptionless.Worker'
            },
            '@ref:session': sessionId,
            '@user': {
                identity,
                name
            }
        };

        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, relatedEvent);
        const persistedRelatedEvent = await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, eventReferenceId);
        expect(persistedRelatedEvent.stack_id).toBeTruthy();
        relatedEventId = persistedRelatedEvent.id;
        relatedStackId = persistedRelatedEvent.stack_id!;
    });

    await test.step('open the session from the Sessions table', async () => {
        await page.goto('/next/sessions?time=all');
        await expect(page.getByRole('heading', { name: 'Sessions' })).toBeVisible();

        const sessionRow = getVisibleRow(page, name, identity);
        await expect(sessionRow).toBeVisible({ timeout: 30_000 });
        await sessionRow.click();

        const eventSheet = page.getByRole('dialog', { name: 'Event' });
        await expect(eventSheet).toBeVisible();
        await expect(eventSheet.getByText(name).filter({ visible: true }).first()).toBeVisible();
        await eventSheet.getByRole('link', { name: 'Open details in new window' }).click();

        await expect(page).toHaveURL(/\/next\/(?:event|stack\/[^/]+\/event)\//);
        await expect(getVisibleText(page, identity)).toBeVisible();
    });

    await test.step('session events wrap summaries, show users, and open with the Events All view', async () => {
        await page.getByRole('tab', { name: 'Session Events' }).click();
        await expect(page.getByRole('tab', { name: 'Session Events' })).toHaveAttribute('aria-selected', 'true');
        const summaryHeader = page.getByRole('columnheader', { name: 'Summary' });
        await expect(summaryHeader).toBeVisible({ timeout: 30_000 });

        const sessionEventsTable = summaryHeader.locator('xpath=ancestor::div[@data-slot="table-container"]');
        await expect(sessionEventsTable.getByRole('columnheader')).toHaveText(['Summary', 'User', 'Session Time']);
        await expect(sessionEventsTable.getByTitle(`${name} (${identity})`).first()).toBeVisible();
        await expect.poll(() => sessionEventsTable.evaluate((element) => element.scrollWidth - element.offsetWidth)).toBeLessThanOrEqual(1);

        const eventsLink = page.getByRole('link', { name: 'Open events filtered to this session' });
        await expect(eventsLink).toHaveAttribute('href', /\/next\/event\/all\?/);
        const eventsHref = await eventsLink.getAttribute('href');
        expect(new URL(eventsHref!, page.url()).searchParams.get('filter')).toContain('ref.session');
    });

    await test.step('event detail messages wrap and small alignment fixes render consistently', async () => {
        await page.goto(`/next/stack/${relatedStackId}/event/${relatedEventId}`);
        await expect(page.getByRole('tab', { name: 'Overview' })).toHaveAttribute('aria-selected', 'true');

        const activePanel = () => page.getByRole('tabpanel').filter({ visible: true });
        const activeTable = () => activePanel().locator('[data-slot="table-container"]').first();
        const expectNoHorizontalOverflow = async () => {
            await expect.poll(() => activeTable().evaluate((element) => element.scrollWidth - element.clientWidth)).toBeLessThanOrEqual(2);
        };

        await expectNoHorizontalOverflow();
        await page.getByRole('tab', { name: 'Exception' }).click();
        await expectNoHorizontalOverflow();

        await page.getByRole('tab', { name: 'Environment' }).click();
        const machineNameRow = activePanel().getByRole('row').filter({ hasText: 'Machine Name' });
        await expect(machineNameRow).toBeVisible();
        await expect
            .poll(() =>
                machineNameRow
                    .locator('[data-slot="table-cell"]')
                    .last()
                    .evaluate((element) => getComputedStyle(element).display)
            )
            .toBe('table-cell');

        await page.getByRole('button', { name: 'Stack options' }).click();
        const stackingInformationItem = page.getByRole('menuitem', { name: 'View Stacking Information' });
        await expect(stackingInformationItem).toBeVisible();
        await expect.poll(() => stackingInformationItem.locator('svg').evaluate((element) => getComputedStyle(element).marginRight)).toBe('8px');
        await page.keyboard.press('Escape');

        const sidebarRail = page.locator('[data-slot="sidebar-rail"]');
        await expect(sidebarRail).toBeVisible();
        await expect.poll(() => sidebarRail.evaluate((element) => getComputedStyle(element).cursor)).toBe('pointer');
    });
});
