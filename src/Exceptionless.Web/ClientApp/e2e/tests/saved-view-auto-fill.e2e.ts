import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

test('saved views choose which event or stack column auto-fills', async ({ e2eApi, e2eScenario, page, request }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await journey.submitRepresentativeEvent();

    await test.step('a user can choose and save a non-default auto-fill column', async () => {
        const slugSuffix = journey.run
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-|-$/g, '')
            .slice(-40)
            .replace(/^-|-$/g, '');
        const viewSlug = `e2e-auto-fill-${slugSuffix}`;
        const response = await request.post(`/api/v2/organizations/${e2eScenario.organizationId}/saved-views`, {
            data: {
                columns: {
                    date: { position: 1, visible: true, width: 140 },
                    exception_type: { visible: false },
                    level: { visible: false },
                    message: { visible: false },
                    name: { visible: false },
                    project: { visible: false },
                    source: { visible: false },
                    summary: { position: 0, visible: true, width: 320 },
                    tags: { visible: false },
                    type: { visible: false },
                    user: { visible: false },
                    version: { visible: false }
                },
                name: `E2E Auto-Fill ${slugSuffix}`,
                organization_id: e2eScenario.organizationId,
                slug: viewSlug,
                view_type: 'events'
            },
            headers: { Authorization: `Bearer ${e2eScenario.userToken}` }
        });
        expect(response.status(), await response.text()).toBe(201);

        await page.setViewportSize({ height: 900, width: 1600 });
        await page.goto(`/next/event/${viewSlug}`);
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

        await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();

        const columnDialog = page.getByRole('dialog', { name: 'Column Picker' });
        const dateAutoFill = columnDialog.getByRole('radio', { name: 'Date auto fill' });
        const summaryAutoFill = columnDialog.getByRole('radio', { name: 'Summary auto fill' });
        await expect(columnDialog).toBeVisible();
        await expect(dateAutoFill).not.toBeChecked();
        await dateAutoFill.click();
        await expect(dateAutoFill).toBeChecked();
        await summaryAutoFill.click();
        await expect(summaryAutoFill).toBeChecked();
        await columnDialog.getByRole('button', { name: 'Reset to default' }).click();
        await expect(summaryAutoFill).toBeChecked();
        await dateAutoFill.click();
        await expect(dateAutoFill).toBeChecked();
        await columnDialog.getByRole('button', { name: 'Done' }).click();

        await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
        const saveResponse = page.waitForResponse((candidate) => candidate.request().method() === 'PATCH' && candidate.url().includes(`/api/v2/saved-views/`));
        await page.getByRole('menuitem', { exact: true, name: 'Save' }).click();
        const savedResponse = await saveResponse;
        expect(savedResponse.status()).toBe(200);
        expect(savedResponse.request().postDataJSON().columns.date.auto_fill).toBe(true);
        expect((await savedResponse.json()).columns.date.auto_fill).toBe(true);

        await page.reload();
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });
        await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
        await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();
        await expect(page.getByRole('dialog', { name: 'Column Picker' }).getByRole('radio', { name: 'Date auto fill' })).toBeChecked();
        await page.getByRole('dialog', { name: 'Column Picker' }).getByRole('button', { name: 'Done' }).click();

        const table = page.locator('table:has(thead [role="checkbox"])').first();
        const dateHeader = table.getByRole('columnheader', { name: 'Date' });
        const summaryHeader = table.getByRole('columnheader', { name: 'Summary' });
        const initialTableWidth = (await table.boundingBox())!.width;
        const initialDateWidth = (await dateHeader.boundingBox())!.width;
        const initialSummaryWidth = (await summaryHeader.boundingBox())!.width;

        await page.setViewportSize({ height: 900, width: 2000 });
        await expect.poll(async () => (await table.boundingBox())!.width).toBeGreaterThan(initialTableWidth + 300);

        const widerTableWidth = (await table.boundingBox())!.width;
        const widerDateWidth = (await dateHeader.boundingBox())!.width;
        const widerSummaryWidth = (await summaryHeader.boundingBox())!.width;

        expect(widerSummaryWidth).toBeCloseTo(initialSummaryWidth, 1);
        expect(widerDateWidth - initialDateWidth).toBeCloseTo(widerTableWidth - initialTableWidth, 1);
    });

    for (const route of ['/next/event/all', '/next/stack/all']) {
        await test.step(`${route} predefined view auto-fills Summary`, async () => {
            await page.setViewportSize({ height: 900, width: 1600 });
            await page.goto(route);
            await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

            const table = page.locator('table:has(thead [role="checkbox"])').first();
            const border = table.locator('xpath=ancestor::div[contains(@class, "rounded-md") and contains(@class, "border")][1]');
            const summaryHeader = table.getByRole('columnheader', { name: 'Summary' });
            const narrowTableWidth = (await table.boundingBox())!.width;
            const narrowBorderWidth = (await border.boundingBox())!.width;
            const narrowSummaryWidth = (await summaryHeader.boundingBox())!.width;

            expect(Math.abs(narrowTableWidth - narrowBorderWidth)).toBeLessThanOrEqual(2);

            await page.setViewportSize({ height: 900, width: 2000 });
            await expect.poll(async () => (await table.boundingBox())!.width).toBeGreaterThan(narrowTableWidth + 300);

            const wideTableWidth = (await table.boundingBox())!.width;
            const wideBorderWidth = (await border.boundingBox())!.width;
            const wideSummaryWidth = (await summaryHeader.boundingBox())!.width;

            expect(Math.abs(wideTableWidth - wideBorderWidth)).toBeLessThanOrEqual(2);
            expect(wideSummaryWidth - narrowSummaryWidth).toBeCloseTo(wideTableWidth - narrowTableWidth, 1);
        });
    }
});
