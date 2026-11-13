import { createUsageChartData } from '../../src/lib/features/organizations/components/usage-chart.fixtures';
import { expect, test } from '../fixtures/e2e-test';

test.use({ locale: 'en-US', timezoneId: 'UTC' });

test.afterEach(async ({ page }) => {
    await page.unrouteAll({ behavior: 'wait' });
    await page.close();
});

test('usage charts preserve labels, hover values, and legend interactions', async ({ e2eScenario, page }) => {
    // Arrange
    const data = createUsageChartData();
    const labels = ['Total', 'Discarded', 'Blocked', 'Too Big', 'Deleted', 'Limit'];
    const keys = ['total', 'discarded', 'blocked', 'too_big', 'deleted', 'limit'] as const;
    await page.route(
        (url) => url.pathname === `/api/v2/organizations/${e2eScenario.organizationId}`,
        async (route) => {
            const response = await route.fetch();
            await route.fulfill({
                json: {
                    ...(await response.json()),
                    max_events_per_month: 4_000_000,
                    plan_name: 'Sample',
                    usage: data.map(({ organization_total, ...usage }) => ({ ...usage, total: organization_total }))
                },
                response
            });
        }
    );
    await page.route(
        (url) => url.pathname === `/api/v2/projects/${e2eScenario.projectId}`,
        async (route) => {
            const response = await route.fetch();
            await route.fulfill({ json: { ...(await response.json()), usage: data }, response });
        }
    );

    for (const kind of ['organization', 'project'] as const) {
        await test.step(`${kind}: labels and raw hover values`, async () => {
            // Arrange
            const project = kind === 'project';
            const expectedLabels = project ? ['Total in Organization', ...labels] : labels;
            const chart = page.locator('[data-chart]');
            const tooltip = page.locator('.lc-tooltip-container');

            // Act
            await page.goto(`/${kind}/${project ? e2eScenario.projectId : e2eScenario.organizationId}/usage`);

            // Assert
            await expect(page.getByText('View your historical usage')).toBeVisible();
            await expect(page.getByText('You are currently on the')).toContainText('Sample plan');
            await expect(page.getByText('The usage data above is refreshed periodically and may not reflect current totals.')).toBeVisible();
            await expect(chart.getByRole('button')).toHaveText(expectedLabels);
            await expect(chart.locator('.lc-area-path')).toHaveCount(expectedLabels.length);

            for (const index of [0, 6, 11]) {
                // Arrange
                const datum = data[index];
                const baseline = await chart.locator('.lc-rule-y-line').last().boundingBox();
                if (!baseline) throw new Error('Missing chart baseline');
                const fraction = (datum.date.getTime() - data[0].date.getTime()) / (data[11].date.getTime() - data[0].date.getTime());
                const values = keys.map((key) => (key === 'total' && !project ? datum.organization_total : datum[key]));
                if (project) values.unshift(datum.organization_total);

                // Act
                await page.mouse.move(baseline.x + Math.max(1, Math.min(baseline.width - 1, fraction * baseline.width)), baseline.y - 50);

                // Assert
                await expect(tooltip).toBeVisible();
                await expect(tooltip).toContainText(
                    datum.date.toLocaleDateString('en-US', { day: 'numeric', month: 'long', timeZone: 'UTC', year: 'numeric' })
                );
                await expect(tooltip.locator('span.text-muted-foreground')).toHaveText([...expectedLabels].reverse());
                await expect(tooltip.locator('span.font-mono')).toHaveText(values.reverse().map((value) => value.toLocaleString('en-US')));
            }
        });

        await test.step(`${kind}: legend toggles preserve independent series`, async () => {
            // Arrange
            const chart = page.locator('[data-chart]');
            const expectedLabels = kind === 'project' ? ['Total in Organization', ...labels] : labels;

            for (const label of expectedLabels) {
                // Arrange
                const button = chart.getByRole('button', { exact: true, name: label });

                // Act
                await button.focus();
                await button.press('Space');

                // Assert
                await expect(chart.locator('.lc-area-path')).toHaveCount(1);
                if (kind === 'project' && label === 'Limit') {
                    await expect
                        .poll(() => chart.locator('path.usage-limit-line:not(.lc-area-path)').evaluate((path) => (path as SVGGraphicsElement).getBBox().y))
                        .toBeLessThan(0.1);
                }

                // Act
                await button.press('Space');

                // Assert
                await expect(chart.locator('.lc-area-path')).toHaveCount(expectedLabels.length);
            }
        });

        await test.step(`${kind}: limit stays within the plot with its existing stroke`, async () => {
            // Arrange
            const chart = page.locator('[data-chart]');
            const limit = chart.locator('path.usage-limit-line:not(.lc-area-path)');

            // Act
            const bounds = await limit.boundingBox();
            const chartBounds = await chart.boundingBox();

            // Assert
            await expect(limit).toHaveCSS('stroke-dasharray', 'none');
            expect(bounds).not.toBeNull();
            expect(chartBounds).not.toBeNull();
            expect(await limit.evaluate((path) => (path as SVGGraphicsElement).getBBox().height)).toBeLessThan(0.1);
            expect(bounds!.y).toBeGreaterThanOrEqual(chartBounds!.y);
        });
    }
});
