import { expect, test } from '../fixtures/e2e-test';

test('creates an isolated organization and verifies event ingestion in the app', async ({ authenticatedPage: page, e2eApi, e2eScenario }) => {
    const event = await e2eApi.getEvent(e2eScenario.userToken, e2eScenario.event.id);

    expect(event.id).toBe(e2eScenario.event.id);
    expect(event.reference_id).toBe(e2eScenario.referenceId);

    await page.goto('/next');
    await expect(page.getByRole('heading', { name: 'Events' })).toBeVisible();
    await expect(page.getByText(e2eScenario.event.message)).toBeVisible({ timeout: 30_000 });

    await page.goto('/next/issues');
    await expect(page.getByRole('heading', { name: 'Issues' })).toBeVisible();

    await page.goto('/next/stream');
    await expect(page.getByRole('heading', { name: 'Event Stream' })).toBeVisible();
    await expect(page.getByText(e2eScenario.event.message)).toBeVisible({ timeout: 30_000 });

    await page.goto(`/next/event/${e2eScenario.event.id}`);
    await expect(page.getByRole('heading', { name: 'Event Details' })).toBeVisible();
    await expect(page.getByTitle(e2eScenario.event.message, { exact: true })).toBeVisible({ timeout: 30_000 });
});
