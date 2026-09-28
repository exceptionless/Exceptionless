import { expect, test } from '../fixtures/e2e-test';

const syntheticProjects = Array.from({ length: 32 }, (_, index) => ({
    id: index.toString(16).padStart(24, '0'),
    name: `Synthetic Project ${String(index + 1).padStart(2, '0')}`,
    organization_id: 'synthetic-organization'
}));

test('project dropdown layout preserves its search and footer while listing many projects', async ({ e2eScenario, page }, testInfo) => {
    // Arrange
    await page.route(
        (url) => url.pathname.includes(`/organizations/${e2eScenario.organizationId}/projects`),
        async (route) => await route.fulfill({ json: syntheticProjects })
    );

    await page.setViewportSize({ height: 720, width: 1280 });
    await page.goto('/next/stack?time=all');
    await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible();
    await expect(page.getByText('No data was found with the current filter.', { exact: true })).toBeVisible();

    const projectFilter = page.getByRole('button', { name: /^Project/ }).first();
    await expect(projectFilter).toBeVisible();
    // Act
    await projectFilter.click();

    // Assert
    const popover = page.locator('[data-slot="popover-content"]:visible').last();
    const search = popover.getByRole('combobox');
    const commandList = popover.locator('[data-slot="command-list"]');
    const footer = popover.locator('#Project-help + div');

    await expect(search).toBeVisible();
    await expect(commandList).toBeVisible();
    await expect(footer).toBeVisible();
    const screenshotPath = testInfo.outputPath('project-dropdown.png');
    await page.screenshot({ fullPage: false, path: screenshotPath });
    await testInfo.attach('project-dropdown.png', { contentType: 'image/png', path: screenshotPath });

    const metrics = await popover.evaluate((element) => {
        const searchInput = element.querySelector('[data-slot="command-input"]');
        const list = element.querySelector('[data-slot="command-list"]');
        const footer = element.querySelector('[id$="-help"] + div');
        const bounds = element.getBoundingClientRect();
        const footerBounds = footer?.getBoundingClientRect();

        return {
            bottom: bounds.bottom,
            footerBottom: footerBounds?.bottom,
            footerVisible: footerBounds ? footerBounds.bottom <= window.innerHeight : false,
            height: bounds.height,
            listClientHeight: list?.clientHeight,
            listScrollHeight: list?.scrollHeight,
            searchBottom: searchInput?.getBoundingClientRect().bottom,
            top: bounds.top,
            viewportHeight: window.innerHeight
        };
    });

    expect(metrics.top).toBeGreaterThanOrEqual(8);
    expect(metrics.bottom).toBeLessThanOrEqual(metrics.viewportHeight - 8);
    expect(metrics.height).toBeLessThanOrEqual(metrics.viewportHeight);
    expect(metrics.footerVisible).toBe(true);
    expect(metrics.listClientHeight).toBeGreaterThan(288);
    expect(metrics.listClientHeight).toBeLessThanOrEqual(384);
    expect(metrics.listScrollHeight).toBeGreaterThan(metrics.listClientHeight);

    // Act
    await search.fill('synthetic project 32');

    // Assert
    await expect(popover.getByRole('option', { name: 'Synthetic Project 32' })).toBeVisible();
    const filteredListMetrics = await commandList.evaluate((element) => ({ clientHeight: element.clientHeight, scrollHeight: element.scrollHeight }));
    expect(filteredListMetrics.clientHeight).toBeLessThan(metrics.listClientHeight!);
    expect(filteredListMetrics.scrollHeight).toBeLessThanOrEqual(filteredListMetrics.clientHeight);

    // Act
    await search.fill('');
    await search.press('ArrowDown');
    await search.press('Enter');
    // Assert
    await expect(popover).toBeVisible();
    // Act
    await page.keyboard.press('Escape');
    // Assert
    await expect(popover).toBeHidden();
    await testInfo.attach('project-dropdown-metrics.json', {
        body: JSON.stringify({ filteredListMetrics, metrics }, null, 2),
        contentType: 'application/json'
    });
});

test('project dropdown keeps its controls visible in mobile viewports and a 200% browser-zoom-equivalent CSS viewport', async ({ e2eScenario, page }) => {
    // Arrange
    await page.route(
        (url) => url.pathname.includes(`/organizations/${e2eScenario.organizationId}/projects`),
        async (route) => await route.fulfill({ json: syntheticProjects })
    );

    const listHeights: number[] = [];
    for (const viewport of [
        { compareListHeight: true, height: 400, label: 'short mobile', width: 390 },
        { compareListHeight: true, height: 844, label: 'tall mobile', width: 390 },
        { compareListHeight: false, height: 360, label: '200% browser zoom equivalent (640x360 CSS viewport)', width: 640 }
    ]) {
        // Arrange
        await page.setViewportSize({ height: viewport.height, width: viewport.width });
        await page.goto('/next/stack?time=all');
        await expect(page.getByRole('heading', { name: 'Stacks' })).toBeVisible();
        await expect(page.getByText('No data was found with the current filter.', { exact: true })).toBeVisible();

        const projectFilter = page.getByRole('button', { name: /^Project/ }).first();
        await expect(projectFilter).toBeVisible();
        // Act
        await projectFilter.click();

        // Assert
        const popover = page.locator('[data-slot="popover-content"]:visible').last();
        await expect(popover.getByRole('combobox')).toBeVisible();
        await expect(popover.locator('[data-slot="command-list"]')).toBeVisible();
        await expect(popover.getByRole('button', { name: 'Remove filter' })).toBeVisible();
        const metrics = await popover.evaluate((element) => {
            const list = element.querySelector('[data-slot="command-list"]');
            const footer = element.querySelector('[id$="-help"] + div');
            const bounds = element.getBoundingClientRect();
            const footerBounds = footer?.getBoundingClientRect();

            return {
                bottom: bounds.bottom,
                footerBottom: footerBounds?.bottom,
                footerVisible: footerBounds ? footerBounds.bottom <= window.innerHeight : false,
                listClientHeight: list?.clientHeight,
                listScrollHeight: list?.scrollHeight,
                top: bounds.top,
                viewportHeight: window.innerHeight
            };
        });

        expect(metrics.top, `${viewport.label} popover top`).toBeGreaterThanOrEqual(8);
        expect(metrics.bottom, `${viewport.label} popover bottom`).toBeLessThanOrEqual(metrics.viewportHeight - 8);
        expect(metrics.footerVisible).toBe(true);
        expect(metrics.listClientHeight).toBeGreaterThan(0);
        expect(metrics.listScrollHeight).toBeGreaterThan(metrics.listClientHeight);
        if (viewport.compareListHeight) {
            listHeights.push(metrics.listClientHeight!);
        }

        // Act
        await page.keyboard.press('Escape');
        // Assert
        await expect(popover).toBeHidden();
    }

    expect(listHeights[0]).toBeLessThan(listHeights[1]);
});
