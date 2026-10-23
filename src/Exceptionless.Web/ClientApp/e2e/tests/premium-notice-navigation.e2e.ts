import { expect, type Page, test } from '@playwright/test';

import { mockTablePage } from '../support/table-layout';

test.afterEach(async ({ page }) => {
    expect(await page.pageErrors()).toEqual([]);
});

test('the Sessions premium notice stays visible when Back returns to an earlier filter', async ({ page }) => {
    // Arrange
    await page.clock.install();
    await mockTablePage(page, 0, { hasPremiumFeatures: false });
    await page.goto('/sessions');
    const notice = page.getByText('to enable Sessions and other premium features!');
    await expect(notice).toBeVisible();

    // Act: each filter change adds a query history entry to the same page, and Back returns to one.
    await selectDateRange(page, 'Last 7 days', 'Last 30 days');
    // Rapid changes share one entry, so let the first one settle.
    await page.clock.fastForward(250);
    await selectDateRange(page, 'Last 30 days', 'Last 24 hours');
    await page.goBack();

    // Assert
    await expect(page).toHaveURL(/[?&]time=30d(?:&|$)/);
    await expect(dateFilter(page, 'Last 30 days')).toBeVisible();
    await expect(notice).toBeVisible();
});

function dateFilter(page: Page, range: string) {
    return page.getByRole('button', { exact: true, name: `Date ${range}` });
}

async function selectDateRange(page: Page, from: string, to: string): Promise<void> {
    await dateFilter(page, from).click();
    await page.getByRole('button', { exact: true, name: to }).click();
    await expect(dateFilter(page, to)).toBeVisible();
}
