import type { ProductTourUsageResponse } from '../../src/lib/generated/api';

import { expect, test } from '../fixtures/e2e-test';

test('real dashboard matches repository totals across daily, month, and history filters', async ({ e2eApi, page }, testInfo) => {
    // Arrange: use the real local API, including its empty/unavailable-storage response.
    const token = await e2eApi.login();
    await page.addInitScript((token) => localStorage.setItem('satellizer_token', token), token);

    // Act & Assert
    for (const period of ['Last 30 days', 'Show month', 'Available history']) {
        const pending = page.waitForResponse((response) => new URL(response.url()).pathname === '/api/v2/admin/product-tour-usage');
        if (period === 'Last 30 days') {
            await page.goto('/next/system/product-tours');
        } else {
            await page.getByRole('button', { name: /^Usage period:/ }).click();
            await page.getByRole('button', { exact: true, name: period }).click();
        }
        const response = await pending;
        expect(response.status()).toBe(200);
        const usage: ProductTourUsageResponse = await response.json();
        const overview = usage.tours.find((tour) => tour.name === 'app-overview');
        expect(overview).toBeDefined();
        const totals = page.getByLabel('Explore Exceptionless usage', { exact: true }).getByRole('list', { name: 'Period totals' });
        await expect(totals).toContainText(`Started ${overview!.started}`);
        await expect(totals).toContainText(`Completed ${overview!.completed}`);
        await expect(totals).toContainText(`Dismissed ${overview!.dismissed}`);
        await expect(page.getByText('Guide activity', { exact: true })).toBeVisible();
        await expect(page.getByText('Failed to load guided-tour usage. Please try again.')).toHaveCount(0);
        await page.screenshot({ animations: 'disabled', path: testInfo.outputPath(`local-api-${period.toLowerCase().replaceAll(' ', '-')}.png`) });
    }
});

test('synthetic activity charts support keyboard, compact ranges, and light/dark layouts', async ({ e2eApi, page }, testInfo) => {
    // Arrange: isolated response fixture; no synthetic events are written to storage.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const token = await e2eApi.login();
    await page.addInitScript((token) => localStorage.setItem('satellizer_token', token), token);
    const today = new Date();
    today.setUTCHours(0, 0, 0, 0);
    const activity = Array.from({ length: 30 }, (_, index) => ({
        completed: 3 + (index % 5),
        date_utc: new Date(today.getTime() - (29 - index) * 86_400_000).toISOString(),
        dismissed: index % 3,
        shown: 15 + (index % 9),
        started: 8 + (index % 7)
    }));
    const sum = (key: 'completed' | 'dismissed' | 'shown' | 'started') => activity.reduce((total, day) => total + day[key], 0);
    await page.route('**/api/v2/admin/product-tour-usage*', (route) => {
        const history = new URL(route.request().url()).searchParams.get('history') === 'true';
        const periods = history
            ? activity.map((period, index) => ({
                  ...period,
                  date_utc: new Date(today.getTime() - (30 - index) * 6 * 3_600_000).toISOString()
              }))
            : activity;
        return route.fulfill({
            json: {
                collection_available: true,
                interval: history ? 'auto' : 'day',
                tours: ['app-overview', 'project-configure', 'saved-view-create', 'app-welcome'].map((name) => ({
                    activity: periods,
                    completed: sum('completed'),
                    dismissed: sum('dismissed'),
                    kind: name === 'app-welcome' ? 'prompt' : 'guide',
                    last_run_utc: new Date().toISOString(),
                    name,
                    shown: sum('shown'),
                    start_sources: [{ count: sum('started'), source: 'catalog' }],
                    started: sum('started'),
                    steps: [{ dismissed: 3, reached: 20, step: 'navigation' }],
                    version: 1
                })),
                utc_end: new Date().toISOString(),
                utc_start: periods[0].date_utc
            }
        });
    });

    // Act & Assert
    await page.goto('/next/system/product-tours');
    await expect(page.getByRole('button', { name: 'Usage period: Last 30 days' })).toBeVisible();
    const chart = page.getByRole('slider').first();
    await chart.focus();
    await page.keyboard.press('Home');
    await expect(chart).toHaveAttribute('aria-valuenow', '0');
    await expect(chart).toHaveAttribute('aria-valuetext', /Started: 8.*Completed: 3.*Dismissed: 0/);
    await page.keyboard.press('ArrowRight');
    await expect(chart).toHaveAttribute('aria-valuenow', '1');
    await expect(chart).toHaveAttribute('aria-valuetext', /Started: 9/);
    await expect(page.getByRole('tooltip')).toBeVisible();
    await expect(page.getByRole('tooltip').getByText('9', { exact: true })).toBeVisible();
    await page.keyboard.press('Tab');
    await expect(page.getByText('Most common exit:', { exact: false })).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Steps and entry points' })).toHaveCount(0);
    const details = page.getByRole('button', { name: 'Explore Exceptionless activity details' });
    await details.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByRole('dialog', { exact: true, name: 'Explore Exceptionless' })).toBeVisible();
    await expect(page.getByRole('list', { name: 'Guide entry points' })).toContainText('100.0%');
    await expect(page.getByText('Most common exit: navigation')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(details).toBeFocused();
    await page.getByRole('button', { name: 'Usage period: Last 30 days' }).click();
    await page.getByRole('button', { exact: true, name: 'Available history' }).click();
    await expect(page.getByRole('button', { name: 'Usage period: Available history' })).toBeVisible();
    await expect(page.getByText('Guide activity', { exact: true })).toBeVisible();
    await expect(chart).toHaveAttribute('aria-valuemax', '29');
    await chart.focus();
    await page.keyboard.press('Home');
    await expect(chart).toHaveAttribute('aria-valuetext', /Started: 8.*Completed: 3/);
    await page.keyboard.press('ArrowRight');
    await expect(chart).toHaveAttribute('aria-valuetext', /Started: 9/);
    await expect(page.getByRole('tooltip').getByText('9', { exact: true })).toBeVisible();
    await page.keyboard.press('Tab');
    await page.evaluate(() => {
        const label = document.createElement('div');
        label.textContent = 'SYNTHETIC LOCAL FIXTURE — NOT CUSTOMER ACTIVITY';
        label.style.cssText =
            'position:fixed;bottom:8px;left:8px;z-index:9999;background:#111;color:#fff;padding:6px 10px;font:10px sans-serif;border-radius:4px';
        document.body.append(label);
    });
    for (const [theme, width] of [
        ['dark', 1440],
        ['light', 1440],
        ['dark', 390]
    ] as const) {
        await page.setViewportSize({ height: 960, width });
        await page.evaluate((theme) => {
            document.documentElement.classList.toggle('dark', theme === 'dark');
            document.documentElement.classList.toggle('light', theme === 'light');
        }, theme);
        await expect(chart).toBeVisible();
        await expect
            .poll(async () => {
                const labels = await chart.locator('.lc-axis-tick-label').evaluateAll((elements) =>
                    elements.map((element) => ({
                        bounds: element.getBoundingClientRect().toJSON(),
                        text: element.textContent
                    }))
                );
                const zero = labels.find((label) => label.text === '0');
                const firstDate = labels.find((label) => label.text?.match(/[A-Za-z]/));
                return zero && firstDate ? firstDate.bounds.top - zero.bounds.bottom : 0;
            })
            .toBeGreaterThanOrEqual(6);
        await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
        await page.screenshot({ animations: 'disabled', path: testInfo.outputPath(`synthetic-chart-${theme}-${width}.png`) });
        await details.click();
        const popover = page.getByRole('dialog', { exact: true, name: 'Explore Exceptionless' });
        await expect(popover).toBeVisible();
        await expect
            .poll(async () => {
                const bounds = await popover.boundingBox();
                return bounds !== null && bounds.x >= 16 && bounds.x + bounds.width <= width - 16;
            })
            .toBe(true);
        await page.screenshot({ animations: 'disabled', path: testInfo.outputPath(`synthetic-details-${theme}-${width}.png`) });
        await page.keyboard.press('Escape');
    }
});
