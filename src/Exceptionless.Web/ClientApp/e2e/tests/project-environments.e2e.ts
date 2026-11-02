import { createReferenceId, expect, test } from '../fixtures/e2e-test';

test('operator can list, configure, and filter environments and allow discarded names', async ({ e2eApi, e2eScenario, page }) => {
    const { projectId, projectToken, run, userToken } = e2eScenario;

    await test.step('seed events from environments that are not listed yet', async () => {
        for (const [index, environment] of ['production', 'Staging'].entries()) {
            const referenceId = createReferenceId(run, `-managed-env-${index}`);
            await e2eApi.submitEvent(projectId, projectToken, {
                environment,
                message: `Managed environment ${environment} ${run}`,
                reference_id: referenceId,
                source: 'managed-environment-test',
                type: 'log'
            });
            await e2eApi.pollForEventByReference(userToken, projectId, referenceId);
        }
    });

    await test.step('observed environments appear as unlisted rows', async () => {
        await page.goto(`/project/${projectId}/environments`);

        await expect(page.getByRole('heading', { name: 'Environments in Use' })).toBeVisible();
        await expect(page.getByRole('row', { name: /^production Not listed/ })).toBeVisible({ timeout: 30_000 });
        await expect(page.getByRole('row', { name: /^staging Not listed/ })).toBeVisible();
    });

    await test.step('discarding unlisted environments asks for confirmation while nothing is listed', async () => {
        const discard = page.getByRole('switch', { name: 'Discard events from environments that are not listed' });
        await discard.click();

        const dialog = page.getByRole('alertdialog');
        await expect(dialog.getByText(/every event that has an environment will be discarded/)).toBeVisible();
        await dialog.getByRole('button', { name: 'Cancel' }).click();
        await expect(dialog).toBeHidden();
        await expect(discard).not.toBeChecked();
    });

    await test.step('adding a name lists it in the casing that was typed and merges the observed row', async () => {
        await page.getByLabel('Environment name').fill('  Production ');
        await page.getByRole('button', { name: 'Add environment' }).click();

        await expect(page.getByText('Added Production.')).toBeVisible();
        await expect(page.getByRole('row', { name: /^Production Listed/ })).toBeVisible();
        await expect(page.getByRole('row', { name: /^production Not listed/ })).toBeHidden();

        await page.getByLabel('Environment name').fill('PRODUCTION');
        await page.getByRole('button', { name: 'Add environment' }).click();
        await expect(page.getByText('PRODUCTION is already listed. Names are not case-sensitive.')).toBeVisible();
    });

    await test.step('adding an observed name pre-fills the form so the casing can be corrected', async () => {
        await page.getByRole('button', { name: 'Add staging' }).click();
        await expect(page.getByLabel('Environment name')).toHaveValue('staging');
        await expect(page.getByRole('row', { name: /^staging Not listed/ })).toBeVisible();

        await page.getByLabel('Environment name').fill('Staging');
        await page.getByRole('button', { name: 'Add environment' }).click();
        await expect(page.getByRole('row', { name: /^Staging Listed/ })).toBeVisible();
    });

    await test.step('notifications can be turned off for a listed environment and persist', async () => {
        const notify = page.getByRole('switch', { name: 'Send notifications for Production' });
        await expect(notify).toBeChecked();
        const saved = page.waitForResponse(
            (response) => response.request().method() === 'PATCH' && response.url().includes(`/projects/${projectId}/environments`)
        );
        await notify.click();
        expect((await saved).ok()).toBe(true);

        await page.reload();
        await expect(page.getByRole('switch', { name: 'Send notifications for Production' })).not.toBeChecked();
    });

    await test.step('event filters offer the listed casing', async () => {
        await page.goto(`/event?project=${projectId}&time=all`);
        await page.getByRole('button', { name: 'Manage filters' }).click();
        await page.getByPlaceholder('Search...').fill('Environment');
        await page.getByText('Environment', { exact: true }).click();

        await expect(page.getByRole('option', { exact: true, name: 'Production' })).toBeVisible();
        await expect(page.getByRole('option', { exact: true, name: 'Staging' })).toBeVisible();
        await page.keyboard.press('Escape');
    });

    await test.step('discarding unlisted environments reports the names that were rejected', async () => {
        await page.goto(`/project/${projectId}/environments`);
        const discard = page.getByRole('switch', { name: 'Discard events from environments that are not listed' });
        const settingsSaved = page.waitForResponse((response) => response.request().method() === 'PUT' && response.url().endsWith('/environments/settings'));
        await discard.click();
        expect((await settingsSaved).ok()).toBe(true);
        await expect(page.getByRole('switch', { name: 'Also discard events that have no environment' })).toBeEnabled();

        const referenceId = createReferenceId(run, '-managed-env-typo');
        await e2eApi.submitEvent(projectId, projectToken, {
            environment: 'Prodution',
            message: `Managed environment typo ${run}`,
            reference_id: referenceId,
            source: 'managed-environment-test',
            type: 'log'
        });

        await expect(async () => {
            await page.reload();
            await expect(page.getByRole('button', { name: 'Allow Prodution' })).toBeVisible({ timeout: 2_000 });
        }).toPass({ timeout: 60_000 });
    });

    await test.step('allowing a discarded name lists it and clears it from the discarded events', async () => {
        await page.getByRole('button', { name: 'Allow Prodution' }).click();

        await expect(page.getByText('Added Prodution.')).toBeVisible();
        await expect(page.getByRole('row', { name: /^Prodution Listed/ })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Allow Prodution' })).toBeHidden();
    });

    await test.step('removing a listed name keeps its events', async () => {
        await page.getByRole('button', { name: 'Remove Prodution' }).click();
        await page.getByRole('button', { name: 'Remove Environment' }).click();

        await expect(page.getByText('Removed Prodution. Its events were not changed.')).toBeVisible();
        await expect(page.getByRole('row', { name: /^Prodution Listed/ })).toBeHidden();
    });
});
