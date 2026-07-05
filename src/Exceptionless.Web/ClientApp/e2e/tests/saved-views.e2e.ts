import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

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
        await expect(page.getByText(journey.message).first()).toBeVisible({ timeout: 30_000 });

        await page.getByRole('button', { name: 'View' }).click();
        await page.getByRole('menuitem', { name: 'Save As...' }).click();

        const dialog = page.getByRole('dialog', { name: 'Save View' });
        await expect(dialog).toBeVisible();
        await dialog.getByLabel('Name').fill(viewName);
        await expect(dialog.getByLabel('URL name')).toHaveValue(viewSlug);
        await dialog.getByRole('button', { name: 'Save' }).click();

        await expect(page.getByRole('heading', { name: viewName })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
        await expect(page.getByText(journey.message).first()).toBeVisible();
    });

    await test.step('rename the saved view without breaking the active route', async () => {
        await page.getByRole('button', { name: 'View' }).click();
        await page.getByRole('menuitem', { name: 'Rename' }).click();

        const dialog = page.getByRole('dialog', { name: 'Rename View' });
        await expect(dialog).toBeVisible();
        await dialog.getByLabel('Name').fill(renamedViewName);
        await dialog.getByLabel('URL name').fill(viewSlug);
        await dialog.getByRole('button', { name: 'Rename' }).click();

        await expect(page.getByRole('heading', { name: renamedViewName })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(new RegExp(`/next/event/${escapeRegExp(viewSlug)}(?:[?#]|$)`));
        await expect(page.getByText(journey.message).first()).toBeVisible();
    });

    await test.step('delete the saved view and return to the default Events view', async () => {
        await page.getByRole('button', { name: 'View' }).click();
        await page.getByRole('menuitem', { name: `Delete "${renamedViewName}"` }).click();

        await expect(page.getByRole('heading', { name: 'Delete Saved View' })).toBeVisible();
        await page.getByRole('button', { name: 'Delete' }).click();

        await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible({ timeout: 30_000 });
        await expect(page).toHaveURL(/\/next\/event(?:[?#]|$)/);
        await expect(page.getByRole('heading', { name: renamedViewName })).toHaveCount(0);
    });
});

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function savedViewSlug(value: string): string {
    return value
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '')
        .replace(/-+/g, '-');
}
