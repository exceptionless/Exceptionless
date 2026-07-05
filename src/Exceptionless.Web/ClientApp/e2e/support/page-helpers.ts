import { expect, type Locator, type Page } from '@playwright/test';

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

export async function selectProjectType(page: Page, optionName: string): Promise<void> {
    await page.getByRole('button', { name: /Please select a project type|Command Line:/ }).click();
    const option = page.getByRole('option', { name: optionName });

    try {
        await option.click({ timeout: 5_000 });
    } catch {
        await page.keyboard.press('Enter');
    }
}

export async function waitForEmailValidation(page: Page): Promise<void> {
    await page
        .getByLabel('Validating email')
        .waitFor({ state: 'detached', timeout: 10_000 })
        .catch(() => undefined);
}
