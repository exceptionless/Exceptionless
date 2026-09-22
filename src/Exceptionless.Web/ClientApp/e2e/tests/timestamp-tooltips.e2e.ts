import { expect, test } from '../fixtures/e2e-test';

for (const [locale, timezoneId] of [
    ['en-US', 'America/Los_Angeles'],
    ['en-GB', 'Europe/London'],
    ['de-DE', 'Europe/Berlin'],
    ['ja-JP', 'Asia/Tokyo'],
    ['ar-EG', 'Africa/Cairo']
]) {
    test.describe(`${locale} in ${timezoneId}`, () => {
        test.use({ locale, timezoneId });

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
                const expected = await page.evaluate(
                    ({ locale, timezoneId, value }) =>
                        new Intl.DateTimeFormat(locale, {
                            dateStyle: 'medium',
                            timeStyle: 'long',
                            timeZone: timezoneId
                        }).format(new Date(value)),
                    { locale, timezoneId, value: date.toISOString() }
                );
                await expect(timestamp).toHaveAttribute('title', expected);
                await expect(timestamp.locator('.sr-only')).toContainText(expected);
                const accessibleText = await timestamp.ariaSnapshot();
                expect(accessibleText).toContain(expected);
                await expect(timestamp).not.toHaveAttribute('tabindex');
            }
        });
    });
}
