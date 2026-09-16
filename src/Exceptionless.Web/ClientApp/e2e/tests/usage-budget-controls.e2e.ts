import { expect, test } from '@playwright/test';

const ORGANIZATION_ID = '000000000000000000000001';
const PROJECT_ID = '000000000000000000000003';

for (const width of [1280, 390]) {
    test.describe(`usage budget controls at ${width}px`, () => {
        test.use({ viewport: { height: 900, width } });
        test.setTimeout(30_000);

        for (const resource of ['organization', 'project'] as const) {
            test(`${resource} budget validates, saves, and clears settings`, async ({ page }, testInfo) => {
                const organization = {
                    budget_alert_settings: { enabled: true, thresholds: [50, 80] },
                    features: [],
                    id: ORGANIZATION_ID,
                    max_events_per_month: 1000,
                    name: 'Budget Organization',
                    plan_id: 'EX_FREE',
                    plan_name: 'Free',
                    usage: []
                };
                const project = {
                    effective_ingest_limit: 100,
                    id: PROJECT_ID,
                    ingest_limit: { fixed_limit: 100, type: 0 },
                    name: 'Budget Project',
                    organization_id: ORGANIZATION_ID,
                    usage: []
                };
                const errors: string[] = [];
                const updates: unknown[] = [];
                let rejectUpdate = true;
                page.on('pageerror', (error) => errors.push(error.message));
                await page.addInitScript((organizationId) => {
                    localStorage.setItem('satellizer_token', 'usage-budget-test-token');
                    localStorage.setItem('organization', JSON.stringify(organizationId));
                }, ORGANIZATION_ID);
                await page.route('**/health', (route) => route.fulfill({ body: 'OK' }));
                await page.routeWebSocket('**/api/v2/push**', () => {});
                await page.route('**/api/v2/**', async (route) => {
                    const path = new URL(route.request().url()).pathname;
                    if (route.request().method() === 'PATCH') {
                        const update = route.request().postDataJSON();
                        updates.push(update);
                        if (!rejectUpdate) {
                            const model = resource === 'project' ? project : organization;
                            Object.assign(model, update);
                            await route.fulfill({ json: model });
                            return;
                        }

                        await route.fulfill({
                            contentType: 'application/problem+json',
                            json: {
                                errors: { [resource === 'project' ? 'ingest_limit' : 'budget_alert_settings']: ['Budget settings were rejected.'] },
                                status: 422,
                                title: 'Validation failed'
                            },
                            status: 422
                        });
                        return;
                    }

                    if (path === '/api/v2/users/me') {
                        await route.fulfill({
                            json: {
                                email_address: 'budget@example.test',
                                full_name: 'Budget Tester',
                                id: '000000000000000000000002',
                                is_active: true,
                                is_email_address_verified: true,
                                organization_ids: [ORGANIZATION_ID],
                                organization_preferences: [],
                                roles: []
                            }
                        });
                    } else if (path === `/api/v2/organizations/${ORGANIZATION_ID}`) {
                        await route.fulfill({ json: organization });
                    } else if (path === `/api/v2/projects/${PROJECT_ID}`) {
                        await route.fulfill({ json: project });
                    } else if (path === '/api/v2/organizations') {
                        await route.fulfill({ json: [organization] });
                    } else if (path.endsWith('/projects')) {
                        await route.fulfill({ json: [project] });
                    } else if (path === '/api/v2/assistant/access') {
                        await route.fulfill({ json: { enabled: false, has_access: false, upgrade_required: false } });
                    } else {
                        await route.fulfill({ json: [] });
                    }
                });

                const id = resource === 'project' ? PROJECT_ID : ORGANIZATION_ID;
                await page.goto(`/next/${resource}/${id}/usage`);
                if (resource === 'project') {
                    await expect(page.getByText('Current project limit', { exact: true })).toBeVisible({ timeout: 2000 });
                }
                const input = page.getByRole('textbox', { name: resource === 'project' ? 'Monthly event count' : 'Threshold percentages' });
                const save = page.getByRole('button', { name: resource === 'project' ? 'Save project budget' : 'Save budget alerts' });
                await input.fill(resource === 'project' ? '1.5' : '75.5');
                await save.click();
                await expect(input).toHaveAttribute('aria-invalid', 'true');
                expect(updates).toEqual([]);

                await input.fill(resource === 'project' ? '200' : '60, 90');
                await save.click();
                await expect(page.getByText('Budget settings were rejected.', { exact: true })).toBeVisible();
                await expect(input).toHaveValue(resource === 'project' ? '200' : '60, 90');
                await page.screenshot({ path: testInfo.outputPath('server-validation.png') });

                rejectUpdate = false;
                await save.click();
                await expect(page.getByText(resource === 'project' ? 'Project event budget saved.' : 'Budget alert settings saved.')).toBeVisible();
                expect(updates.at(-1)).toEqual(
                    resource === 'project'
                        ? { ingest_limit: { fixed_limit: 200, percent_of_organization_limit: null, type: 0 } }
                        : { budget_alert_settings: { enabled: true, thresholds: [60, 90] } }
                );

                if (resource === 'project') {
                    await page.getByRole('button', { exact: true, name: 'Limit type' }).click();
                    await page.getByRole('option', { exact: true, name: 'No project limit' }).click();
                    await save.click();
                    await expect.poll(() => updates.at(-1)).toEqual({ ingest_limit: null });
                } else {
                    await page.getByRole('button', { name: 'Clear settings' }).click();
                    await expect(page.getByText('Budget alert settings cleared.')).toBeVisible();
                    expect(updates.at(-1)).toEqual({ budget_alert_settings: null });
                }

                expect(errors).toEqual([]);
                expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
            });
        }
    });
}
