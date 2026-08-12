import type { Page } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { getVisibleText } from '../support/page-helpers';

test('events saved view can be saved, renamed, loaded, and deleted', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const suffix = journey.run.slice(-36);
    const viewName = `E2E Events ${suffix}`;
    const renamedViewName = `E2E Events Renamed ${suffix}`;
    const viewSlug = savedViewSlug(viewName);

    await test.step('submit a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('save the filtered Events page as a view', async () => {
        await page.goto(`/next/event?reference=${encodeURIComponent(journey.referenceId)}&time=all`);
        await expect(getVisibleText(page, journey.message)).toBeVisible({ timeout: 30_000 });

        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Save As...' }).click();

        const dialog = page.getByRole('dialog', { name: 'Save View' });
        await expect(dialog).toBeVisible();
        await dialog.getByLabel('Name', { exact: true }).fill(viewName);
        await expect(dialog.getByLabel('URL name', { exact: true })).toHaveValue(viewSlug);
        await dialog.getByRole('button', { name: 'Save' }).click();
        await expect(dialog).toBeHidden({ timeout: 30_000 });

        await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });

    await test.step('rename the saved view and keep the saved route active', async () => {
        await openViewMenu(page);
        await page.getByRole('menuitem', { exact: true, name: 'Rename' }).click();

        const dialog = page.getByRole('dialog', { name: 'Rename View' });
        await expect(dialog).toBeVisible();
        await dialog.getByLabel('Name', { exact: true }).fill(renamedViewName);
        await dialog.getByLabel('URL name', { exact: true }).fill(viewSlug);
        await dialog.getByRole('button', { name: 'Rename' }).click();
        await expect(dialog).toBeHidden({ timeout: 30_000 });

        await expect(page.getByRole('heading', { name: renamedViewName })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });

    await test.step('reset route-specific filter overrides to the saved view', async () => {
        await page.goto(`/next/event/${viewSlug}?project=${e2eScenario.projectId}`);
        await openViewMenu(page);
        await page.getByRole('menuitem', { name: 'Reset to Saved' }).click();

        await expect(page.getByRole('menu')).toBeHidden();
        await expect(page).not.toHaveURL(/[?&]project=/);
        await expect(getVisibleText(page, journey.message)).toBeVisible();
    });

    await test.step('delete the saved view and return to the default Events view', async () => {
        await openViewMenu(page);
        await page.getByRole('menuitem', { name: `Delete "${renamedViewName}"` }).click();

        const dialog = page.getByRole('alertdialog', { name: 'Delete Saved View' });
        await expect(dialog).toBeVisible();
        await dialog.getByRole('button', { name: 'Delete' }).click();
        await expect(dialog).toBeHidden({ timeout: 30_000 });

        await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(/\/next\/event(?:[?#]|$)/);
        await expect(page.getByRole('heading', { name: renamedViewName })).toHaveCount(0);
    });
});

test('switching saved views discards temporary filter overrides', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    const suffix = journey.run.slice(-28);
    const firstViewName = `E2E First View ${suffix}`;
    const secondViewName = `E2E Second View ${suffix}`;
    const firstViewSlug = savedViewSlug(firstViewName);
    const secondViewSlug = savedViewSlug(secondViewName);

    await journey.submitRepresentativeEvent();
    await saveView(page, firstViewName, journey.referenceId, '15m');
    await saveView(page, secondViewName, journey.referenceId, '1d');

    await page.goto(`/next/event/${firstViewSlug}`);
    const dateFilter = page.getByRole('button', { name: /^Date/ }).filter({ visible: true }).first();
    await dateFilter.click();
    await page.getByRole('button', { name: 'Last 90 days' }).click();
    await expect(page).toHaveURL(/[?&]time=90d(?:&|$)/);

    await page.getByRole('link', { exact: true, name: secondViewName }).first().click();
    await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(secondViewSlug)}(?:[?#]|$)`));
    await expect(page.getByRole('heading', { name: secondViewName })).toBeVisible();
    await expect(
        page
            .getByRole('button', { name: /Date\s+Last 24 hours/ })
            .filter({ visible: true })
            .first()
    ).toBeVisible();
});

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function openViewMenu(page: Page): Promise<void> {
    await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
}

function savedViewSlug(value: string): string {
    return value
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '')
        .replace(/-+/g, '-');
}

async function saveView(page: Page, viewName: string, referenceId: string, time: string): Promise<void> {
    await page.goto(`/next/event?reference=${encodeURIComponent(referenceId)}&time=${time}`);
    await openViewMenu(page);
    await page.getByRole('menuitem', { name: 'Save As...' }).click();
    const dialog = page.getByRole('dialog', { name: 'Save View' });
    await dialog.getByLabel('Name', { exact: true }).fill(viewName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(dialog).toBeHidden({ timeout: 30_000 });
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
}
