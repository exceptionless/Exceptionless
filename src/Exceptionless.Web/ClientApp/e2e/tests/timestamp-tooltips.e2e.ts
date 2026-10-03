import { expect, test } from '../fixtures/e2e-test';

test('event and stack timestamp titles remain accessible across locales and timezones', async ({ browser, e2eApi, e2eScenario }) => {
    const date = new Date();
    date.setUTCHours(0, 0, 0, 0);
    await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, {
        date: date.toISOString(),
        message: 'Synthetic timestamp verification',
        reference_id: e2eScenario.referenceId,
        type: 'error'
    });
    await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, e2eScenario.referenceId);

    // Share indexed data while preserving a fresh browser context for every locale.
    for (const [locale, timezoneId] of [
        ['en-US', 'America/Los_Angeles'],
        ['en-GB', 'Europe/London'],
        ['de-DE', 'Europe/Berlin'],
        ['ja-JP', 'Asia/Tokyo'],
        ['ar-EG', 'Africa/Cairo']
    ]) {
        await test.step(`${locale} in ${timezoneId}`, async () => {
            const context = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true, locale, timezoneId });
            try {
                await context.addInitScript(
                    ({ organizationId, token }) => {
                        window.localStorage.setItem('satellizer_token', token);
                        window.localStorage.setItem('organization', JSON.stringify(organizationId));
                    },
                    { organizationId: e2eScenario.organizationId, token: e2eScenario.userToken }
                );
                const page = await context.newPage();
                for (const route of ['/event?time=all', '/stack?time=all']) {
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
            } finally {
                await context.close();
            }
        });
    }
});
