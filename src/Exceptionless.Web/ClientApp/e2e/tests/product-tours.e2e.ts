import { expect, test } from '../fixtures/e2e-test';

test('completed and dismissed tours remain replayable from command search', async ({ page }) => {
    await page.goto('/next/stack');

    await startTourFromCommand(page, 'Explore the new UI');
    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Your workspace navigation')).toBeVisible();
    await tour.getByRole('button', { name: 'Close' }).click();
    await expect(tour).toBeHidden();

    await startTourFromCommand(page, 'Explore the new UI');
    await expect(tour.getByText('Your workspace navigation')).toBeVisible();
    await tour.getByRole('button', { name: 'Close' }).click();
});

test('Meet Exie opens contextual UI without sending a provider request', async ({ page }) => {
    let chatRequests = 0;
    await page.route('**/api/v2/assistant/access**', async (route) => {
        await route.fulfill({
            contentType: 'application/json',
            json: { enabled: true, has_access: true, message: null, upgrade_required: false }
        });
    });
    page.on('request', (request) => {
        if (new URL(request.url()).pathname === '/api/v2/assistant/chat') {
            chatRequests += 1;
        }
    });

    await page.goto('/next/stack');
    await startTourFromCommand(page, 'Meet Exie');

    const tour = page.locator('.driver-popover');
    await expect(tour.getByText('Open Exie')).toBeVisible();
    await tour.getByRole('button', { name: 'Continue' }).click();
    await expect(page.getByRole('dialog', { name: 'Exie' })).toBeVisible();
    await expect(tour.getByText('You control every request')).toBeVisible();
    expect(chatRequests).toBe(0);

    await tour.getByRole('button', { name: 'Done' }).click();
    expect(chatRequests).toBe(0);
});

async function startTourFromCommand(page: import('@playwright/test').Page, title: string): Promise<void> {
    await page.getByRole('button', { name: 'Search Exceptionless' }).click();
    await page.getByRole('dialog').getByText(title, { exact: true }).click();
}
