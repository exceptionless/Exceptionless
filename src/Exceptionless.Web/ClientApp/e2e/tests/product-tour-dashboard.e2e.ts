import { expect, test } from '../fixtures/e2e-test';

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
    await page.route('**/api/v2/admin/product-tour-usage*', (route) =>
        route.fulfill({
            json: {
                collection_available: true,
                interval: 'day',
                tours: ['app-overview', 'project-configure', 'saved-view-create', 'app-welcome'].map((name) => ({
                    activity,
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
                utc_start: activity[0].date_utc
            }
        })
    );

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
    await page.keyboard.press('Tab');
    await page.getByRole('button', { name: 'Steps and entry points' }).first().click();
    await expect(page.getByRole('list', { name: 'Guide entry points' })).toContainText('100%');
    await page.keyboard.press('Escape');
    await page.getByRole('button', { name: 'Usage period: Last 30 days' }).click();
    await page.getByRole('button', { exact: true, name: 'Available history' }).click();
    await expect(page.getByRole('button', { name: 'Usage period: Available history' })).toBeVisible();
    await page.getByRole('button', { name: 'Usage period: Available history' }).click();
    await page.getByRole('button', { exact: true, name: 'Last 30 days' }).click();
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
        await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
        await page.screenshot({ animations: 'disabled', path: testInfo.outputPath(`synthetic-chart-${theme}-${width}.png`) });
    }
});
