import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

test('operator can refresh Events after a transient load failure', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    let failedOnce = false;

    await journey.submitRepresentativeEvent();

    await page.route(`**/api/v2/organizations/${e2eScenario.organizationId}/events*`, async (route) => {
        const url = new URL(route.request().url());
        if (!failedOnce && url.pathname.endsWith(`/organizations/${e2eScenario.organizationId}/events`)) {
            failedOnce = true;
            await route.fulfill({
                body: JSON.stringify({ status: 503, title: 'Temporary test failure' }),
                contentType: 'application/problem+json',
                status: 503
            });
            return;
        }

        await route.continue();
    });

    await test.step('encounter a transient Events request failure', async () => {
        await page.goto(`/next/event?reference=${encodeURIComponent(e2eScenario.referenceId)}&time=all`);
        await expect.poll(() => failedOnce).toBeTruthy();
        await expect(page.getByTitle('Refresh results')).toBeVisible();
    });

    await test.step('recover through the visible refresh control', async () => {
        await page.getByTitle('Refresh results').click();
        await expect(getVisibleText(page, e2eScenario.message)).toBeVisible({ timeout: 30_000 });
    });
});
