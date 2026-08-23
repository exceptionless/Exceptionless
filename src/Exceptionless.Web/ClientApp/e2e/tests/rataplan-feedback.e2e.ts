import { createReferenceId, expect, test } from '../fixtures/e2e-test';
import { seedRepresentativeEvent } from '../support/event-data';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';
import { createRepresentativeEvent } from '../support/synthetic-event';

test('approved Rataplan UI feedback remains fixed', async ({ e2eApi, e2eScenario, page }) => {
    const journey = ExceptionlessE2EJourney.fromScenario(page, e2eApi, e2eScenario);
    await journey.submitRepresentativeEvent();

    const firstEventId = journey.eventId!;
    const wrappedTagValues = ['Authentication', 'Background', 'Critical', 'Customer', 'Production', 'Server', 'TeamOne', 'Version2', 'Web'];
    const secondEvent = await seedRepresentativeEvent(e2eApi, e2eScenario.userToken, {
        message: journey.message,
        projectId: e2eScenario.projectId,
        projectToken: e2eScenario.projectToken,
        referenceId: createReferenceId(journey.run, '-second')
    });
    expect(secondEvent.stack_id).toBe(journey.stackId);

    for (let index = 0; index < 5; index++) {
        const referenceId = createReferenceId(journey.run, `-stack-${index}`);
        const event = createRepresentativeEvent({
            appUrl: e2eApi.environment.appUrl,
            message: `${journey.message} stack ${index}`,
            referenceId,
            runId: e2eApi.environment.runId
        });
        if (index === 0) {
            event.tags = wrappedTagValues;
        }

        const data = event.data as Record<string, unknown>;
        const simpleError = data['@simple_error'] as Record<string, unknown>;
        await e2eApi.submitEvent(e2eScenario.projectId, e2eScenario.projectToken, {
            ...event,
            data: {
                ...data,
                '@simple_error': {
                    ...simpleError,
                    type: `PlaywrightRataplanException${index}`
                }
            }
        });
        await e2eApi.pollForEventByReference(e2eScenario.userToken, e2eScenario.projectId, referenceId);
    }

    await test.step('event details use the page scroll and preserve the selected tab when the event changes', async () => {
        await page.goto(`/next/stack/${journey.stackId}/event/${firstEventId}`);
        const overviewTab = page.getByRole('tab', { name: 'Overview' });
        await expect(overviewTab).toHaveAttribute('aria-selected', 'true');

        const stackTrace = page.getByRole('heading', { name: 'Stack Trace' }).locator('xpath=../following-sibling::div[1]');
        await expect(stackTrace).toBeVisible();
        await expect.poll(() => stackTrace.evaluate((element) => element.scrollHeight === element.clientHeight)).toBe(true);

        await page.getByRole('tab', { name: 'Exception' }).click();
        await expect(page.getByRole('tab', { name: 'Exception' })).toHaveAttribute('aria-selected', 'true');

        const newerEventButton = page.getByRole('button', { name: 'Newer event' });
        const olderEventButton = page.getByRole('button', { name: 'Older event' });
        if (await newerEventButton.isEnabled()) {
            await newerEventButton.click();
        } else {
            await olderEventButton.click();
        }

        await expect(page).not.toHaveURL(new RegExp(`/event/${firstEventId}(?:[?#]|$)`));
        await expect(page.getByRole('tab', { name: 'Exception' })).toHaveAttribute('aria-selected', 'true');
    });

    await test.step('command palette overflow is discoverable and shortcut chips have stronger emphasis', async () => {
        await page.keyboard.press('/');
        const commandList = page.locator('[data-slot="command-list"]');
        await expect(commandList).toBeVisible();

        const commandListState = await commandList.evaluate((element) => ({
            hasHiddenScrollbarClass: element.classList.contains('no-scrollbar'),
            hasOverflow: element.scrollHeight > element.clientHeight,
            overflowY: getComputedStyle(element).overflowY,
            scrollbarWidth: getComputedStyle(element).scrollbarWidth
        }));
        expect(commandListState).toEqual({
            hasHiddenScrollbarClass: false,
            hasOverflow: true,
            overflowY: 'auto',
            scrollbarWidth: 'thin'
        });

        const shortcut = commandList.locator('[data-slot="command-shortcut"]').first();
        await expect(shortcut).toBeVisible();
        const shortcutStyle = await shortcut.evaluate((element) => {
            const style = getComputedStyle(element);
            return {
                borderTopWidth: style.borderTopWidth,
                boxShadow: style.boxShadow,
                fontWeight: style.fontWeight
            };
        });
        expect(Number(shortcutStyle.fontWeight)).toBeGreaterThanOrEqual(600);
        expect(Number.parseFloat(shortcutStyle.borderTopWidth)).toBeGreaterThan(0);
        expect(shortcutStyle.boxShadow).not.toBe('none');

        await commandList.hover();
        await commandList.evaluate((element) => (element.scrollTop = 40));
        await page.screenshot({ path: 'dogfood-output/rataplan-command-palette.png' });
        await page.keyboard.press('Escape');
    });

    await test.step('manual refresh keeps the current page', async () => {
        await page.goto(`/next/stack?project=${e2eScenario.projectId}&limit=5&time=all`);
        const pager = page.getByRole('group', { name: 'Table pagination' });
        await expect(pager).toBeVisible();
        await expect(pager.getByRole('button', { name: 'Rows per page' })).toContainText('5 rows');
        await expect(pager.getByLabel('Page 1 of 2')).toBeVisible();
        await expect
            .poll(async () => {
                const pagerBounds = await pager.boundingBox();
                const footerBounds = await pager.locator('xpath=..').boundingBox();
                return pagerBounds && footerBounds ? footerBounds.x + footerBounds.width - (pagerBounds.x + pagerBounds.width) : Number.POSITIVE_INFINITY;
            })
            .toBeLessThanOrEqual(1);

        const nextPageResponse = page.waitForResponse((response) => {
            const url = new URL(response.url());
            return url.pathname.includes('/api/v2/organizations/') && url.pathname.endsWith('/events') && url.searchParams.get('page') === '2';
        });
        await page.getByRole('button', { name: 'Go to next page' }).click();
        expect((await nextPageResponse).ok()).toBe(true);
        await expect(page).toHaveURL(/[?&]page=2(?:&|$)/);
        await expect(pager.getByLabel('Page 2 of 2')).toBeVisible();
        await expect(page.getByRole('button', { name: 'Go to previous page' })).toBeEnabled();
        await expect(page.getByRole('button', { name: 'Go to next page' })).toBeDisabled();

        const refreshResponse = page.waitForResponse((response) => {
            const url = new URL(response.url());
            return url.pathname.includes('/api/v2/organizations/') && url.pathname.endsWith('/events') && url.searchParams.get('page') === '2';
        });
        await page.getByTitle('Refresh results').click();
        expect((await refreshResponse).ok()).toBe(true);
        await expect(page).toHaveURL(/[?&]page=2(?:&|$)/);
        await page.screenshot({ path: 'dogfood-output/rataplan-compact-pager.png' });
    });

    await test.step('row selection clears when navigating between stack views', async () => {
        await page.goto(`/next/stack/all?project=${e2eScenario.projectId}&time=all`);
        const selectedRow = page.getByRole('row').filter({ hasText: journey.message }).first();
        await expect(selectedRow).toBeVisible({ timeout: 30_000 });
        await selectedRow.getByRole('checkbox', { name: 'Select row' }).click();
        await expect(page.getByText('1 selected', { exact: true })).toBeVisible();

        await page.getByRole('link', { exact: true, name: 'Most Frequent Errors' }).filter({ visible: true }).first().click();
        await expect(page).toHaveURL(/\/next\/stack\/most-frequent-errors/);
        await expect(page.getByText(journey.message).filter({ visible: true }).first()).toBeVisible({ timeout: 30_000 });
        await expect(page.getByText('1 selected', { exact: true })).toHaveCount(0);
    });

    await test.step('the fixed-version field has space before the dialog footer', async () => {
        await page.goto(`/next/stack?filter=project:${e2eScenario.projectId}&time=all`);
        await page.getByRole('checkbox', { name: 'Select row' }).first().click();
        const bulkActionsButton = page.getByRole('button', { name: 'Bulk Actions' });
        const selectionCount = page.getByText('1 selected', { exact: true });
        await expect(selectionCount).toBeVisible();
        await expect
            .poll(async () => {
                const buttonBounds = await bulkActionsButton.boundingBox();
                const countBounds = await selectionCount.boundingBox();
                return buttonBounds && countBounds ? countBounds.x - (buttonBounds.x + buttonBounds.width) : Number.POSITIVE_INFINITY;
            })
            .toBeLessThanOrEqual(16);
        await bulkActionsButton.click();
        await expect(page.locator('[data-slot="dropdown-menu-group-heading"]', { hasText: 'Bulk Actions' })).toHaveCount(0);
        await page.getByRole('menuitem', { name: 'Mark Fixed' }).click();

        const versionField = page.getByRole('textbox', { name: 'Version' }).locator('xpath=..');
        await expect(versionField).toBeVisible();
        await expect.poll(() => versionField.evaluate((element) => Number.parseFloat(getComputedStyle(element).paddingBottom))).toBe(16);
        await page.screenshot({ path: 'dogfood-output/rataplan-fixed-version-dialog.png' });
        await page.getByRole('button', { name: 'Cancel' }).click();
    });

    await test.step('message is available as a wrappable event column', async () => {
        await page.goto(`/next/stream?project=${e2eScenario.projectId}&time=all`);
        await expect(page.getByText(journey.message).filter({ visible: true }).first()).toBeVisible({ timeout: 30_000 });

        await page.getByTitle('Manage View Settings').click();
        await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();
        await page.getByRole('button', { name: 'Add Message column' }).click();
        await expect(page.getByRole('checkbox', { name: 'Message wrap text' })).toBeVisible();
        await page.getByRole('button', { name: 'Close' }).click();
    });

    await test.step('renaming a project refreshes cached stack summaries', async () => {
        const stackUrl = `/next/stack?filter=project:${e2eScenario.projectId}&time=all`;
        await page.goto(stackUrl);
        await expect(page.getByText(journey.message).filter({ visible: true }).first()).toBeVisible({ timeout: 30_000 });

        await page.getByTitle('Manage View Settings').click();
        await page.getByRole('menuitem', { name: 'Manage Columns...' }).click();

        const summaryWrapCheckbox = page.getByRole('checkbox', { name: 'Summary wrap text' });
        await expect(summaryWrapCheckbox).not.toBeChecked();
        await summaryWrapCheckbox.click();
        await expect(summaryWrapCheckbox).toBeChecked();
        await expect(page.getByRole('checkbox', { name: 'Status wrap text' })).toHaveCount(0);

        await page.getByRole('button', { name: 'Add Tags column' }).click();
        const tagsWrapCheckbox = page.getByRole('checkbox', { name: 'Tags wrap text' });
        await expect(tagsWrapCheckbox).toBeVisible();
        await tagsWrapCheckbox.click();
        await expect(tagsWrapCheckbox).toBeChecked();
        await expect(page.getByRole('checkbox', { name: /wrap text$/ })).toHaveCount(2);

        const removeColumnButton = page.getByRole('button', { name: /^Remove .+ column$/ }).first();
        await expect(removeColumnButton).toBeVisible();
        await expect
            .poll(async () => {
                const buttonBounds = await removeColumnButton.boundingBox();
                const rowBounds = await removeColumnButton.locator('xpath=..').boundingBox();
                if (!buttonBounds || !rowBounds) {
                    return Number.POSITIVE_INFINITY;
                }

                const buttonCenter = buttonBounds.y + buttonBounds.height / 2;
                const rowCenter = rowBounds.y + rowBounds.height / 2;
                return Math.abs(buttonCenter - rowCenter);
            })
            .toBeLessThanOrEqual(1);
        await page.screenshot({ path: 'dogfood-output/rataplan-column-remove-alignment.png' });

        await page.getByRole('button', { name: 'Add Project column' }).click();
        await page.getByRole('button', { name: 'Close' }).click();

        const tagsResizeHandle = page.getByRole('button', { name: 'Resize tags column' });
        for (let index = 0; index < 12; index++) {
            await tagsResizeHandle.press('ArrowRight');
        }

        const wrappedSummaryCell = page.locator('td[data-wrap="true"]').filter({ hasText: journey.message }).first();
        await expect(wrappedSummaryCell).toBeVisible();
        const wrappedSummaryStyle = await wrappedSummaryCell.evaluate((cell) => {
            const summary = cell.querySelector('.line-clamp-2');
            return {
                lineClamp: summary ? getComputedStyle(summary).webkitLineClamp : undefined,
                whiteSpace: getComputedStyle(cell).whiteSpace
            };
        });
        expect(wrappedSummaryStyle).toEqual({ lineClamp: 'none', whiteSpace: 'normal' });

        const taggedRow = page
            .getByRole('row')
            .filter({ hasText: `${journey.message} stack 0` })
            .first();
        const wrappedTags = taggedRow.getByLabel(`Tags: ${wrappedTagValues.join(', ')}`);
        await expect(wrappedTags).toBeVisible();
        const tagsCell = wrappedTags.locator('xpath=ancestor::td[1]');
        await expect
            .poll(async () => {
                const listBounds = await wrappedTags.boundingBox();
                const cellBounds = await tagsCell.boundingBox();
                return listBounds && cellBounds ? listBounds.width / cellBounds.width : 0;
            })
            .toBeGreaterThan(0.9);
        for (const tag of wrappedTagValues.slice(0, 6)) {
            await expect(wrappedTags.getByRole('button', { exact: true, name: tag })).toBeVisible();
        }
        await expect(wrappedTags.getByText('+3', { exact: true })).toBeVisible();
        await expect(wrappedTags.getByRole('button', { exact: true, name: wrappedTagValues[6] })).toHaveCount(0);
        await page.screenshot({ path: 'dogfood-output/rataplan-wrapped-tags.png' });

        await expect(page.getByRole('cell', { name: journey.projectName }).first()).toBeVisible();

        await page.goto(`/next/project/${e2eScenario.projectId}/manage`);
        const renamedProject = `${journey.projectName} Renamed`;
        const updateResponse = page.waitForResponse(
            (response) => response.url().includes(`/api/v2/projects/${e2eScenario.projectId}`) && response.request().method() === 'PATCH'
        );
        await page.getByLabel('Project name').fill(renamedProject);
        expect((await updateResponse).ok()).toBe(true);

        const refreshedStacks = page.waitForResponse((response) => {
            const url = new URL(response.url());
            return (
                url.pathname.includes('/api/v2/organizations/') &&
                url.pathname.endsWith('/events') &&
                url.searchParams.get('mode')?.startsWith('stack') === true
            );
        });
        await page.goto(stackUrl);
        expect((await refreshedStacks).ok()).toBe(true);
        await expect(page.getByRole('cell', { name: renamedProject }).first()).toBeVisible();
    });
});
