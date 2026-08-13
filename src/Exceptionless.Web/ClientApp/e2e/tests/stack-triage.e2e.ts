import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

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
    await page.getByRole('menuitem', { exact: true, name: 'Ignored' }).click();
    await expect(page.getByText('Status update rejected by test.', { exact: true })).toBeVisible();
});
