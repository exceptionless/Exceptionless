import { resolve } from 'node:path';

import { expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';

test('table toolbar remains accessible while moving between different-height result pages', async ({ e2eApi, e2eScenario, page }) => {
    for (let index = 1; index <= 6; index++) {
        await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
            message: `${e2eScenario.message} ${index}`,
            projectId: e2eScenario.projectId,
            projectToken: e2eScenario.projectToken,
            referenceId: `${e2eScenario.referenceId}-${index}`
        });
    }

    await page.setViewportSize({ height: 720, width: 1280 });
    await page.goto('/next/event?limit=5&time=all');

    const toolbar = page.locator('[data-slot="data-table-footer"]');
    const pager = toolbar.getByRole('navigation', { name: 'Table pagination' });
    const grid = page.locator('[data-slot="data-table-body"]');
    const pageIndicator = pager.getByLabel('Page 1 of 2');
    await expect(pageIndicator).toBeVisible({ timeout: 30_000 });
    expect(await pageIndicator.evaluate((element) => getComputedStyle(element).userSelect)).toBe('none');

    await expect(toolbar.getByRole('button', { name: /Bulk Actions/ })).toBeDisabled();
    await expect(toolbar.getByRole('button', { name: /Bulk Actions/ })).toHaveAttribute('title', 'Select one or more events to use bulk actions');
    await page.locator('tbody').getByRole('checkbox').first().click();
    await expect(toolbar.getByRole('button', { name: /Bulk Actions/ })).toBeEnabled();

    const firstToolbarBox = await toolbar.boundingBox();
    const firstGridBox = await grid.boundingBox();
    expect(firstToolbarBox).not.toBeNull();
    expect(firstGridBox).not.toBeNull();
    expect(await toolbar.evaluate((element) => getComputedStyle(element).position)).toBe('sticky');
    expect(firstToolbarBox!.height).toBeLessThanOrEqual(34);
    expect(firstGridBox!.y - (firstToolbarBox!.y + firstToolbarBox!.height)).toBeLessThanOrEqual(8);
    expect(firstToolbarBox!.y + firstToolbarBox!.height).toBeLessThanOrEqual(firstGridBox!.y);

    if (process.env.E2E_CAPTURE_PAGER_TOOLBAR_SCREENSHOTS === 'true') {
        await page.screenshot({ path: resolve(process.cwd(), '../../../dogfood-output/pager-toolbar/desktop-page-1.png') });
    }

    const nextButton = pager.getByRole('button', { name: 'Go to next page' });
    await nextButton.focus();
    await nextButton.click();
    await expect(pager.getByLabel('Page 2 of 2')).toBeVisible();
    await expect(pager.getByRole('button', { name: 'Go to next page' })).toBeFocused();

    const secondToolbarBox = await toolbar.boundingBox();
    const secondGridBox = await grid.boundingBox();
    expect(secondToolbarBox).not.toBeNull();
    expect(secondGridBox).not.toBeNull();
    expect(secondToolbarBox!.y).toBeGreaterThanOrEqual(8);
    expect(secondToolbarBox!.y + secondToolbarBox!.height).toBeLessThanOrEqual(secondGridBox!.y);

    await page.locator('main').evaluate((element) => element.parentElement?.scrollTo({ top: element.parentElement.scrollHeight }));
    const appFooter = page.getByRole('link', { exact: true, name: 'Terms' }).locator('xpath=ancestor::div[contains(@class, "border-t")][1]');
    await expect(appFooter).toBeVisible();
    const settledToolbarBox = await toolbar.boundingBox();
    const appFooterBox = await appFooter.boundingBox();
    expect(settledToolbarBox).not.toBeNull();
    expect(appFooterBox).not.toBeNull();
    expect(settledToolbarBox!.y + settledToolbarBox!.height).toBeLessThanOrEqual(appFooterBox!.y);

    if (process.env.E2E_CAPTURE_PAGER_TOOLBAR_SCREENSHOTS === 'true') {
        await page.screenshot({ path: resolve(process.cwd(), '../../../dogfood-output/pager-toolbar/desktop-page-2.png') });
    }

    await page.setViewportSize({ height: 720, width: 520 });
    const narrowToolbarBox = await toolbar.boundingBox();
    const narrowGridBox = await grid.boundingBox();
    expect(narrowToolbarBox).not.toBeNull();
    expect(narrowGridBox).not.toBeNull();
    expect(narrowToolbarBox!.x).toBeCloseTo(narrowGridBox!.x, 1);
    expect(narrowToolbarBox!.width).toBeCloseTo(narrowGridBox!.width, 1);

    if (process.env.E2E_CAPTURE_PAGER_TOOLBAR_SCREENSHOTS === 'true') {
        await page.screenshot({ path: resolve(process.cwd(), '../../../dogfood-output/pager-toolbar/narrow-page-2.png') });
    }
});
