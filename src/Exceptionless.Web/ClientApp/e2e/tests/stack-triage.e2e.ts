import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

test('new user can mark an open stack fixed from event details @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);

    await test.step('submit a representative event', async () => {
        await journey.submitRepresentativeEvent();
    });

    await test.step('mark the stack fixed through the UI', async () => {
        await journey.markStackFixed();
    });
});

test('user can restore ignored and discarded stacks through lowercase status requests @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await journey.submitRepresentativeEvent();
    await journey.expectEventDetails();

    const updateStatus = async (menuItem: string, expectedStatus: string, confirmationButton?: string) => {
        await page.getByRole('button', { exact: true, name: /^(Open|Ignored|Discarded)$/ }).click();

        if (confirmationButton) {
            await page.getByRole('menuitem', { exact: true, name: menuItem }).click();
            await expect(page.getByRole('heading', { name: /Discard Stack/ })).toBeVisible();
            const responsePromise = page.waitForResponse((candidate) => candidate.url().includes('/change-status'));
            await page.getByRole('button', { exact: true, name: confirmationButton }).click();
            const response = await responsePromise;
            expect(response.status()).toBe(200);
            expect(new URL(response.request().url()).searchParams.get('status')).toBe(expectedStatus);
        } else {
            const responsePromise = page.waitForResponse((candidate) => candidate.url().includes('/change-status'));
            await page.getByRole('menuitem', { exact: true, name: menuItem }).click();
            const response = await responsePromise;
            expect(response.status()).toBe(200);
            expect(new URL(response.request().url()).searchParams.get('status')).toBe(expectedStatus);
        }

        await expect(page.getByRole('button', { exact: true, name: expectedStatus[0].toUpperCase() + expectedStatus.slice(1) })).toBeVisible();
    };

    await updateStatus('Ignored', 'ignored');
    await updateStatus('Open', 'open');
    await updateStatus('Discarded', 'discarded', 'Discard Stack');
    await updateStatus('Open', 'open');
});

test('status update failure is visible to the user @signup', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await journey.submitRepresentativeEvent();
    await journey.expectEventDetails();

    await page.route('**/api/v2/stacks/*/change-status*', async (route) =>
        route.fulfill({
            body: JSON.stringify({ title: 'Status update rejected by test.' }),
            contentType: 'application/problem+json',
            status: 422
        })
    );

    await page.getByRole('button', { exact: true, name: 'Open' }).click();
    await page.getByRole('menuitem', { exact: true, name: 'Discarded' }).click();
    await expect(page.getByRole('heading', { name: /Discard Stack/ })).toBeVisible();
    await page.getByRole('button', { exact: true, name: 'Discard Stack' }).click();
    await expect(page.getByText('Status update rejected by test.', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: /Discard Stack/ })).toBeVisible();
});

test.describe('email stack actions', () => {
    test.use({ e2eInjectBrowserToken: false, e2eUseGeneratedUser: true });

    test('email shortcuts survive login, require confirmation, and recover from rejected updates', async ({ e2eApi, e2eScenario, page }) => {
        const event = await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, e2eScenario);
        const mutations: string[] = [];
        page.on('request', (request) => {
            if (new URL(request.url()).pathname.includes('/stacks/') && request.method() !== 'GET') mutations.push(request.url());
        });

        await test.step('sign in from a previously delivered action link', async () => {
            await page.goto(`/#!/stack/${event.stack_id}/ignored`);
            await expect(page.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
            await expect.poll(() => new URL(page.url()).searchParams.get('redirect')).toBe(`/stack/${event.stack_id}/ignored`);
            expect(mutations).toEqual([]);
            await page.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
            await page.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
            await page.getByRole('button', { exact: true, name: 'Login' }).click();
            await expect(page.getByRole('heading', { exact: true, name: 'Ignore Stack' })).toBeVisible();
            expect(mutations).toEqual([]);
        });

        for (const action of [
            { button: 'Ignore Stack', heading: 'Ignore Stack', path: 'ignored', request: 'change-status', status: 'Ignored' },
            { button: 'Mark Stack Fixed', heading: 'Mark Stack As Fixed', path: 'mark-fixed', request: 'mark-fixed', status: 'Fixed' },
            { button: 'Discard Stack', heading: 'Discard Stack', path: 'discarded', request: 'change-status', status: 'Discarded' }
        ]) {
            await test.step(`confirm ${action.path} only after recovering from an API rejection`, async () => {
                const before = mutations.length;
                await page.goto(`/stack/${event.stack_id}/${action.path}`);
                const dialog = page.getByRole('alertdialog');
                await expect(dialog.getByRole('heading', { exact: true, name: action.heading })).toBeVisible();
                await expect(dialog.getByText(e2eScenario.message, { exact: true })).toBeVisible();
                expect(mutations).toHaveLength(before);

                if (action.path === 'mark-fixed') {
                    await dialog.getByLabel('Version', { exact: true }).fill('not-a-version');
                    await dialog.getByRole('button', { exact: true, name: action.button }).click();
                    await expect(dialog.getByText('Version must be a valid semantic version (e.g., 1.0.0)', { exact: true })).toBeVisible();
                    expect(mutations).toHaveLength(before);
                    await dialog.getByLabel('Version', { exact: true }).fill('1.2.3');
                }
                const endpoint = `**/api/v2/stacks/${event.stack_id}/${action.request}*`;
                await page.route(endpoint, (route) =>
                    route.fulfill({
                        body: JSON.stringify({ title: 'Email action rejected by test.' }),
                        contentType: 'application/problem+json',
                        status: 422
                    })
                );
                await dialog.getByRole('button', { exact: true, name: action.button }).click();
                await expect(page.getByText('Email action rejected by test.', { exact: true })).toBeVisible();
                await expect(dialog).toBeVisible();
                expect(mutations).toHaveLength(before + 1);
                await page.unroute(endpoint);

                const responsePromise = page.waitForResponse(
                    (response) =>
                        response.request().method() === 'POST' && new URL(response.url()).pathname === `/api/v2/stacks/${event.stack_id}/${action.request}`
                );
                await dialog.getByRole('button', { exact: true, name: action.button }).click();
                const response = await responsePromise;
                expect(response.status()).toBe(200);
                expect(new URL(response.url()).searchParams.get(action.path === 'mark-fixed' ? 'version' : 'status')).toBe(
                    action.path === 'mark-fixed' ? '1.2.3' : action.path
                );
                await expect(dialog).not.toBeVisible();
                await expect(page).toHaveURL(new RegExp(`/stack/${event.stack_id}(?:/event/${event.id})?$`));
                await page.reload();
                await expect(page.getByRole('button', { exact: true, name: action.status })).toBeVisible();
                expect(mutations).toHaveLength(before + 2);
            });
        }

        await test.step('an inaccessible stack never offers confirmation', async () => {
            const before = mutations.length;
            const stackRequests: string[] = [];
            page.on('request', (request) => {
                if (new URL(request.url()).pathname === '/api/v2/stacks/000000000000000000000001') stackRequests.push(request.url());
            });
            await page.goto('/stack/000000000000000000000001/ignored');
            await expect(page.getByText('Unable to load this stack.', { exact: true })).toBeVisible();
            await expect(page.getByRole('alertdialog')).not.toBeVisible();
            expect(stackRequests).toHaveLength(1);
            expect(mutations).toHaveLength(before);
        });
    });
});
