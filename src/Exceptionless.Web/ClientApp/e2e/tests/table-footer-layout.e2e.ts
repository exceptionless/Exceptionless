import { expect, type Page, test } from '@playwright/test';

import { mockTablePage } from '../support/table-layout';

const EMPTY_MESSAGE = 'No data was found with the current filter.';

test.afterEach(async ({ page }) => {
    expect(await page.pageErrors()).toEqual([]);
});

for (const route of ['stack', 'event', 'sessions']) {
    for (const rowCount of [0, 1]) {
        test(`${route} with ${rowCount} rows keeps the app footer visible without scrolling`, async ({ page }) => {
            await mockTablePage(page, rowCount);

            for (const viewport of [
                { height: 800, width: 1280 },
                { height: 1080, width: 1920 },
                { height: 844, width: 390 }
            ]) {
                await page.setViewportSize(viewport);
                await page.goto(`/${route}`);
                if (rowCount === 0) {
                    await expect(page.getByText(EMPTY_MESSAGE, { exact: true })).toBeVisible();
                } else {
                    await expect(page.locator('tbody > tr:visible')).toHaveCount(rowCount);
                    await expect(page.getByText(EMPTY_MESSAGE, { exact: true })).toBeHidden();
                }

                await expectFooterWithoutScrolling(page);
                await expect(page.getByRole('toolbar', { name: 'Table controls' })).not.toHaveAttribute('data-floating');
                await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
            }
        });
    }
}

test('long tables keep sticky controls and release extra scroll space after paging to a short result', async ({ page }) => {
    await mockTablePage(page, 21);
    await page.setViewportSize({ height: 800, width: 1280 });
    await page.goto('/stack');

    const toolbar = page.getByRole('toolbar', { name: 'Table controls' });
    const pager = toolbar.getByRole('navigation', { name: 'Table pagination' });
    const scrollContainer = page.locator('main').locator('..');
    await expect(pager.getByLabel('Page 1 of 2')).toBeVisible();
    await expect(page.locator('tbody > tr:visible')).toHaveCount(20);

    await scrollContainer.evaluate((element) => element.scrollTo({ top: element.scrollHeight / 2 }));
    await expect(toolbar).toHaveAttribute('data-floating', '');
    await expect(toolbar).toBeInViewport({ ratio: 1 });

    const nextButton = pager.getByRole('button', { name: 'Go to next page' });
    await nextButton.click();
    await expect(pager.getByLabel('Page 2 of 2')).toBeVisible();
    await expect(page.locator('tbody > tr:visible')).toHaveCount(1);
    await expect(nextButton).toBeFocused();
    await expectFooterWithoutScrolling(page);
    await expect(toolbar).not.toHaveAttribute('data-floating');
    const firstRowBox = await page.locator('tbody > tr:visible').first().boundingBox();
    const shortToolbarBox = await toolbar.boundingBox();
    expect(firstRowBox!.y).toBeGreaterThanOrEqual(shortToolbarBox!.y + shortToolbarBox!.height);
    expect(firstRowBox!.y + firstRowBox!.height).toBeLessThanOrEqual(page.viewportSize()!.height);

    await pager.getByRole('button', { name: 'Go to previous page' }).click();
    await expect(pager.getByLabel('Page 1 of 2')).toBeVisible();
    await expect(page.locator('tbody > tr:visible')).toHaveCount(20);
    await expect.poll(() => scrollContainer.evaluate((element) => element.scrollTop)).toBe(0);

    await scrollContainer.evaluate((element) => element.scrollTo({ top: element.scrollHeight / 2 }));
    await expect(toolbar).toHaveAttribute('data-floating', '');
    await pager.getByLabel('Rows per page').click();
    await page.getByRole('option', { exact: true, name: '5 rows' }).click();
    await expect(pager.getByLabel('Page 1 of 5')).toBeVisible();
    await expect(page.locator('tbody > tr:visible')).toHaveCount(5);
    await expectFooterWithoutScrolling(page);

    await page.setViewportSize({ height: 360, width: 640 });
    await scrollContainer.evaluate((element) => element.scrollTo({ top: element.scrollHeight }));
    const footer = appFooter(page);
    await expect(footer).toBeInViewport({ ratio: 1 });
    const footerBox = await footer.boundingBox();
    const toolbarBox = await toolbar.boundingBox();
    expect(toolbarBox!.y + toolbarBox!.height).toBeLessThanOrEqual(footerBox!.y);
});

function appFooter(page: Page) {
    return page.getByRole('link', { exact: true, name: 'Terms' }).locator('xpath=ancestor::div[contains(@class, "border-t")][1]');
}

async function expectFooterWithoutScrolling(page: Page): Promise<void> {
    const scrollContainer = page.locator('main').locator('..');
    await expect.poll(() => scrollContainer.evaluate((element) => element.scrollHeight - element.clientHeight)).toBeLessThanOrEqual(1);
    await expect.poll(() => scrollContainer.evaluate((element) => element.scrollTop)).toBe(0);
    await expect(appFooter(page)).toBeInViewport({ ratio: 1 });
    const footerBox = await appFooter(page).boundingBox();
    expect(footerBox!.y + footerBox!.height).toBeCloseTo(page.viewportSize()!.height, 0);
}
