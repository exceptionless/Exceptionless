import { expect, test } from '../fixtures/e2e-test';

test.use({ locale: 'en-US', timezoneId: 'UTC' });

test('event and stack timestamps include the full date in the native hover title', async ({ e2eApi, e2eScenario, page }) => {
    const date = new Date();
    date.setUTCHours(0, 0, 0, 0);
    await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, {
        date: date.toISOString(),
        message: 'Synthetic timestamp verification',
        reference_id: e2eScenario.referenceId,
        type: 'error'
    });
    await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, e2eScenario.referenceId);

    for (const route of ['/next/event?time=all', '/next/stack?time=all']) {
        await page.goto(route);
        const timestamp = page.locator('time').first();
        await expect(timestamp).toBeVisible();
        await timestamp.hover();
        await expect(timestamp).toHaveAttribute('title', /12:00:00 AM UTC\+00:00$/);
        await expect(timestamp).toHaveAttribute('title', new RegExp(String(date.getUTCFullYear())));
        await expect(timestamp).not.toHaveAttribute('tabindex');
    }
});
