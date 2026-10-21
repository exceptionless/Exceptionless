import { expect, type Locator, type Page } from '@playwright/test';
import { parse } from 'devalue';

export function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** Reads the `page.state` that SvelteKit stored with the current history entry. */
export async function getHistoryPageState(page: Page): Promise<Record<string, unknown> | undefined> {
    const serialized = await page.evaluate(() => (window.history.state as null | { 'sveltekit:metadata'?: { state?: string } })?.['sveltekit:metadata']?.state);
    return serialized ? (parse(serialized) as Record<string, unknown>) : undefined;
}

export function getIdFromUrl(page: Page, pattern: RegExp): string {
    const match = pattern.exec(new URL(page.url()).pathname);
    if (!match?.[1]) {
        throw new Error(`Could not extract id from ${page.url()}`);
    }

    return match[1];
}

export async function getProjectTokenFromConfigurePage(page: Page): Promise<string> {
    const text = await page.locator('body').innerText();
    const match = /Authorization: Bearer ([A-Za-z0-9_-]+)/.exec(text);
    if (!match?.[1] || match[1] === 'YOUR_API_KEY') {
        throw new Error('Configure page did not expose a project token.');
    }

    return match[1];
}

export async function getUserToken(page: Page): Promise<string> {
    await expect.poll(async () => page.evaluate(() => window.localStorage.getItem('satellizer_token')), { timeout: 30_000 }).toBeTruthy();
    const token = await page.evaluate(() => window.localStorage.getItem('satellizer_token'));
    if (!token) {
        throw new Error('Signup did not persist an access token.');
    }

    return token;
}

export function getVisibleRow(page: Page, ...texts: Array<RegExp | string>): Locator {
    let row = page.getByRole('row');

    for (const text of texts) {
        row = row.filter({ hasText: text });
    }

    return row.filter({ visible: true }).first();
}

export function getVisibleText(page: Page, text: RegExp | string): Locator {
    return page.getByText(text).filter({ visible: true }).first();
}

export async function navigateToSidebarView(page: Page, parentName: string, childName: string): Promise<void> {
    const parentLink = page.getByRole('link', { exact: true, name: parentName }).filter({ visible: true }).first();
    if (await parentLink.isVisible()) {
        await parentLink.click();
        return;
    }

    const childLink = page.getByRole('link', { exact: true, name: childName }).filter({ visible: true }).first();
    if (!(await childLink.isVisible())) {
        await page.getByRole('button', { exact: true, name: parentName }).filter({ visible: true }).first().click();
    }

    await childLink.click();
}

/** Picks a platform tile on the client setup page, for example "REST API (curl)". */
export async function selectProjectType(page: Page, platformName: string): Promise<void> {
    // A chosen platform collapses to one row; reopen the grid to pick another.
    const change = page.getByRole('button', { name: /^Change platform from / });
    if (await change.isVisible()) {
        await change.click();
    }

    await page
        .locator('[data-tour="project-configure-platform"]')
        .getByRole('button', { name: new RegExp(`^${escapeRegExp(platformName)}(?:\\s|$)`) })
        .click();
    await expect(page.getByRole('heading', { name: `Connect ${platformName}` })).toBeVisible();
}

export async function waitForEmailValidation(page: Page): Promise<void> {
    await page
        .getByLabel('Validating email')
        .waitFor({ state: 'detached', timeout: 10_000 })
        .catch(() => undefined);
}
