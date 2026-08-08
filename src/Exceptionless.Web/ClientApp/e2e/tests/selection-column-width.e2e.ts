import { resolve } from 'node:path';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

test('row selection column stays fixed at desktop and narrow widths', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await journey.submitRepresentativeEvent();

    await page.goto(`/next/event?reference=${encodeURIComponent(journey.referenceId)}&time=all`);
    await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

    const table = page.locator('table:has(thead [role="checkbox"])').first();
    const eventRow = table.locator('tbody tr').filter({ has: getVisibleText(page, journey.message) });
    await expect(eventRow).toBeVisible();
    const selectionWidths: number[] = [];

    for (const viewport of [
        { height: 900, name: 'desktop', width: 1440 },
        { height: 900, name: 'narrow', width: 520 }
    ]) {
        await test.step(`${viewport.name} selection column is 32px`, async () => {
            await page.setViewportSize({ height: viewport.height, width: viewport.width });

            const tableBox = await table.boundingBox();
            const selectHeaderBox = await table.locator('thead th').first().boundingBox();
            const selectCellBox = await eventRow.locator('td').first().boundingBox();
            const summaryHeaderBox = await table.locator('thead th').nth(1).boundingBox();

            expect(tableBox).not.toBeNull();
            expect(selectHeaderBox).not.toBeNull();
            expect(selectCellBox).not.toBeNull();
            expect(summaryHeaderBox).not.toBeNull();
            selectionWidths.push(selectHeaderBox!.width);
            expect(selectHeaderBox!.width).toBeCloseTo(32, 1);
            expect(selectCellBox!.width).toBeCloseTo(selectHeaderBox!.width, 1);
            expect(summaryHeaderBox!.x - tableBox!.x).toBeCloseTo(selectHeaderBox!.width, 1);

            if (process.env.E2E_CAPTURE_SELECTION_COLUMN_SCREENSHOTS === 'true') {
                const border = table.locator('xpath=ancestor::div[contains(@class, "rounded-md") and contains(@class, "border")][1]');
                const borderBox = await border.boundingBox();
                expect(borderBox).not.toBeNull();

                await page.screenshot({
                    clip: {
                        height: Math.min(borderBox!.height, 760),
                        width: Math.min(borderBox!.width, viewport.width - borderBox!.x),
                        x: borderBox!.x,
                        y: borderBox!.y
                    },
                    path: resolve(process.cwd(), `../../../dogfood-output/selection-column-width/${viewport.name}.png`)
                });
            }

            if (viewport.name === 'desktop') {
                const summaryHeader = table.locator('thead th').nth(1);
                const summaryWidthBeforeResize = (await summaryHeader.boundingBox())!.width;
                const resizeHandle = summaryHeader.getByRole('button', { name: 'Resize summary column' });
                await resizeHandle.press('ArrowRight');
                await expect.poll(async () => (await summaryHeader.boundingBox())!.width).toBeGreaterThan(summaryWidthBeforeResize);
                await resizeHandle.press('ArrowLeft');
            }
        });
    }

    expect(selectionWidths[0]).toBeCloseTo(selectionWidths[1]!, 1);
});
