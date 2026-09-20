import { expect, test } from '../fixtures/e2e-test';

test.use({ locale: 'en-US', timezoneId: 'UTC' });

test('event and stack timestamps expose full time through keyboard navigation and hover', async ({ e2eApi, e2eScenario, page }) => {
    // Arrange
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
        await test.step(`Tab to the timestamp on ${route}`, async () => {
            // Arrange
            await page.goto(route);
            const timestamp = page.locator('time[data-slot="tooltip-trigger"]').first();
            await expect(timestamp).toBeVisible();

            // Act
            for (let attempt = 0; attempt < 100; attempt++) {
                await page.keyboard.press('Tab');
                if (await timestamp.evaluate((element) => element === document.activeElement)) {
                    break;
                }
            }

            // Assert
            await expect(timestamp).toBeFocused();
            await expect(timestamp).not.toHaveAttribute('title');
            await expect(page.getByRole('tooltip')).toContainText('12:00:00 AM UTC');
            await expect(page.getByRole('tooltip')).toContainText(String(date.getUTCFullYear()));
        });

        await test.step('Escape dismisses the tooltip and hover reopens it', async () => {
            // Arrange
            const timestamp = page.locator('time[data-slot="tooltip-trigger"]').first();

            // Act
            await page.keyboard.press('Escape');

            // Assert
            await expect(page.getByRole('tooltip')).toBeHidden();

            // Act
            await page.keyboard.press('Tab');
            await timestamp.hover();

            // Assert
            await expect(page.getByRole('tooltip')).toContainText('12:00:00 AM UTC');
        });
    }
});
