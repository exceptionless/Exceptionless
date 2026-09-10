import { createReferenceId, expect, test } from '../fixtures/e2e-test';
import { getVisibleText } from '../support/page-helpers';

test('environment filters include unspecified events and persist in saved views', async ({ e2eApi, e2eScenario, page }) => {
    const environments = ['production', 'staging', undefined];
    const messages = environments.map((environment) => `Environment ${environment ?? 'unspecified'} ${e2eScenario.run}`);
    for (const [index, environment] of environments.entries()) {
        const referenceId = createReferenceId(e2eScenario.run, `-env-${index}`);
        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, {
            environment,
            message: messages[index],
            reference_id: referenceId,
            source: 'environment-filter-test',
            type: 'log'
        });
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId);
    }

    await page.goto(`/next/event?project=${e2eScenario.projectId}&time=all`);
    for (const message of messages) await expect(getVisibleText(page, message)).toBeVisible({ timeout: 30_000 });

    await page.getByRole('button', { name: 'Manage filters' }).click();
    await page.getByPlaceholder('Search...').fill('Environment');
    await page.getByText('Environment', { exact: true }).click();
    await page.getByRole('option', { exact: true, name: 'production' }).click();
    await page.keyboard.press('Escape');
    await expect(getVisibleText(page, messages[0]!)).toBeVisible();
    await expect(getVisibleText(page, messages[1]!)).toBeHidden();
    await expect(getVisibleText(page, messages[2]!)).toBeHidden();

    await page.getByRole('button', { name: /^Environment\s+production/ }).click();
    await page.getByRole('option', { exact: true, name: 'Unspecified' }).click();
    await page.keyboard.press('Escape');
    await expect(getVisibleText(page, messages[2]!)).toBeVisible();
    await expect(getVisibleText(page, messages[1]!)).toBeHidden();

    const viewName = `Environments ${e2eScenario.run.slice(-24)}`;
    await page.getByRole('button', { name: /^View/ }).filter({ visible: true }).first().click();
    await page.getByRole('menuitem', { name: 'Save As...' }).click();
    const dialog = page.getByRole('dialog', { name: 'Save View' });
    await dialog.getByLabel('Name', { exact: true }).fill(viewName);
    await expect(dialog.getByLabel('Name', { exact: true })).toHaveValue(viewName);
    const savedResponse = page.waitForResponse((response) => response.request().method() === 'POST' && response.url().includes('/saved-views'));
    await dialog.getByRole('button', { name: 'Save' }).click();
    const response = await savedResponse;
    expect(response.ok(), await response.text()).toBe(true);
    await expect(dialog).toBeHidden();
    await expect(page.getByRole('heading', { name: viewName })).toBeVisible();
    await page.reload();
    await expect(getVisibleText(page, messages[0]!)).toBeVisible();
    await expect(getVisibleText(page, messages[2]!)).toBeVisible();
    await expect(getVisibleText(page, messages[1]!)).toBeHidden();

    await page.getByRole('button', { name: /^Environment\s/ }).click();
    await page.getByPlaceholder('Environment', { exact: true }).fill(' Preview-West ');
    await page.getByRole('option', { name: 'Use preview-west' }).click();
    await page.keyboard.press('Escape');
    await page.reload();
    await page.getByRole('button', { name: /^Environment\s/ }).click();
    await expect(page.getByRole('option', { exact: true, name: 'preview-west' })).toBeVisible();
    await page.getByRole('button', { name: 'Remove filter' }).click();
    for (const message of messages) await expect(getVisibleText(page, message)).toBeVisible();
    await page.reload();
    for (const message of messages) await expect(getVisibleText(page, message)).toBeVisible();
});

test('stack, session, and stream filters retain names with no current events', async ({ e2eScenario, page }) => {
    for (const route of ['stack', 'sessions', 'stream']) {
        await page.goto(`/next/${route}?project=${e2eScenario.projectId}`);
        await page.getByRole('button', { name: 'Manage filters' }).click();
        await page.getByPlaceholder('Search...').fill('Environment');
        await page.getByText('Environment', { exact: true }).click();
        await page.getByPlaceholder('Environment', { exact: true }).fill('qa,east');
        await page.getByRole('option', { name: 'Use qa,east' }).click();
        await page.keyboard.press('Escape');
        await expect(page.getByRole('button', { name: /^Environment\s+qa,east/ })).toBeVisible();
        await expect.poll(() => new URL(page.url()).searchParams.get(route === 'stream' ? 'filters' : 'environment')).toContain('qa,east');
        await page.reload();
        await expect(page.getByRole('button', { name: /^Environment\s+qa,east/ })).toBeVisible();
    }
});
