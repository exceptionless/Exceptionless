import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { createRepresentativeEvent } from '../support/synthetic-event';

test('project stack management uses cursor pagination without page numbers @signup', async ({ e2eApi, e2eScenario, page }) => {
    const events = Array.from({ length: 6 }, (_, index) => {
        const referenceId = `pw-stack-management-${e2eScenario.run}-${index}`;
        const event = createRepresentativeEvent({
            appUrl: e2eApi.environment.appUrl,
            message: `Project stack management ${e2eScenario.run} ${index}`,
            referenceId,
            runId: e2eScenario.run
        });
        const simpleError = (event.data as Record<string, unknown>)['@simple_error'] as Record<string, unknown>;
        simpleError.type = `ProjectStackManagementException${index}`;
        simpleError.stack_trace = `Error: ${referenceId}\n    at stack-management-${index}.ts:${index + 1}:1`;
        return { event, referenceId };
    });

    await Promise.all(events.map(({ event }) => e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, event)));
    await Promise.all(events.map(({ referenceId }) => e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId)));

    const listResponse = page.waitForResponse((response) => new URL(response.url()).pathname === `/api/v2/projects/${e2eScenario.projectId}/stacks`);
    await page.goto(`/next/project/${e2eScenario.projectId}/stacks?filter=status%3Aopen&limit=5`);
    expect((await listResponse).ok()).toBe(true);
    await expect(page.getByText('Manage project stacks, including restoring ignored or discarded stacks')).toBeVisible();
    await expect(page.locator('tbody tr:visible')).toHaveCount(5);

    await page.getByRole('button', { name: 'Go to next page' }).click();
    await expect(page).toHaveURL(/(?:\?|&)after=[^&]+(?:&|$)/);
    await expect(page).not.toHaveURL(/(?:\?|&)page=/);
    await expect(page.locator('tbody tr:visible').first()).toBeVisible();

    await page.getByRole('button', { name: 'Go to previous page' }).click();
    await expect(page).not.toHaveURL(/(?:\?|&)(?:before|after|page)=/);
    await expect(page.locator('tbody tr:visible')).toHaveCount(5);
});

test('new user can mark an open stack fixed from event details @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);

    await test.step('submit a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('mark the stack fixed through the UI', async () => {
        await journey.markStackFixed();
    });
});

test('user can restore ignored and discarded stacks through lowercase status requests @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await journey.submitRepresentativeEvent();
    await journey.expectEventDetails();

    const updateStatus = async (menuItem: string, expectedStatus: string, confirmationButton?: string) => {
        await page.getByRole('button', { exact: true, name: /^(Open|Ignored|Discarded)$/ }).click();

        if (confirmationButton) {
            await page.getByRole('menuitem', { exact: true, name: menuItem }).click();
            await expect(page.getByRole('heading', { name: /Discard Stack/ })).toBeVisible();
            const responsePromise = page.waitForResponse((candidate) => candidate.url().includes('/change-status'));
            await page.getByRole('button', { exact: true, name: confirmationButton }).click();
            const response = await responsePromise;
            expect(response.status()).toBe(200);
            expect(new URL(response.request().url()).searchParams.get('status')).toBe(expectedStatus);
        } else {
            const responsePromise = page.waitForResponse((candidate) => candidate.url().includes('/change-status'));
            await page.getByRole('menuitem', { exact: true, name: menuItem }).click();
            const response = await responsePromise;
            expect(response.status()).toBe(200);
            expect(new URL(response.request().url()).searchParams.get('status')).toBe(expectedStatus);
        }

        await expect(page.getByRole('button', { exact: true, name: expectedStatus[0].toUpperCase() + expectedStatus.slice(1) })).toBeVisible();
    };

    await updateStatus('Ignored', 'ignored');
    await updateStatus('Open', 'open');
    await updateStatus('Discarded', 'discarded', 'Discard Stack');
    await updateStatus('Open', 'open');
});

test('status update failure is visible to the user @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await journey.submitRepresentativeEvent();
    await journey.expectEventDetails();

    await page.route('**/api/v2/stacks/*/change-status*', async (route) =>
        route.fulfill({
            body: JSON.stringify({ title: 'Status update rejected by test.' }),
            contentType: 'application/problem+json',
            status: 422
        })
    );

    await page.getByRole('button', { exact: true, name: 'Open' }).click();
    await page.getByRole('menuitem', { exact: true, name: 'Discarded' }).click();
    await expect(page.getByRole('heading', { name: /Discard Stack/ })).toBeVisible();
    await page.getByRole('button', { exact: true, name: 'Discard Stack' }).click();
    await expect(page.getByText('Status update rejected by test.', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Discard Stack/ })).toBeVisible();
});
