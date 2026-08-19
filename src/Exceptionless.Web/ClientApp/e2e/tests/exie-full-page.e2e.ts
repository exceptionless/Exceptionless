import { expect, test } from '../fixtures/e2e-test';

test('Exie opens from navigation and expands without losing the conversation', async ({ e2eScenario, page }) => {
    await page.route('**/api/v2/assistant/access**', async (route) => {
        await route.fulfill({
            body: JSON.stringify({ enabled: true, has_access: true, upgrade_required: false }),
            contentType: 'application/json',
            status: 200
        });
    });
    await page.route('**/api/v2/assistant/chat', async (route) => {
        await route.fulfill({
            body: `${JSON.stringify({ text: 'This conversation followed you into the full-page chat.', type: 'text_delta' })}\n${JSON.stringify({ type: 'done' })}\n`,
            contentType: 'text/event-stream',
            status: 200
        });
    });

    await page.goto('/next/event?filter=status%3Aopen');
    await expect(page.getByText(e2eScenario.organizationName, { exact: true })).toBeVisible();

    await test.step('open the full-page chat without rendering the outgoing page in the Exie layout', async () => {
        const outgoingPageTitle = page.getByRole('heading', { exact: true, name: 'Events' });
        await expect(outgoingPageTitle).toBeVisible();
        await outgoingPageTitle.evaluate((element) => {
            element.dataset.outgoingPageTitle = '';
        });
        await page.evaluate(() => {
            document.documentElement.dataset.exieTransitionOverlap = 'false';
            const monitorUntil = performance.now() + 1_000;
            const monitorTransition = () => {
                const outgoingTitle = document.querySelector<HTMLElement>('[data-outgoing-page-title]');
                if (window.location.pathname.endsWith('/exie') && outgoingTitle && outgoingTitle.getBoundingClientRect().width > 0) {
                    document.documentElement.dataset.exieTransitionOverlap = 'true';
                }

                if (performance.now() < monitorUntil) {
                    requestAnimationFrame(monitorTransition);
                }
            };
            requestAnimationFrame(monitorTransition);
        });

        await page.getByRole('link', { exact: true, name: 'Exie' }).click();

        await expect(page).toHaveURL(/\/next\/exie(?:[?#]|$)/);
        await expect(page.locator('[data-assistant-page]')).toBeVisible();
        await expect(page.getByRole('log', { name: 'Conversation with Exie' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Clear conversation' })).toBeDisabled();
        await page.waitForTimeout(200);
        expect(await page.locator('html').getAttribute('data-exie-transition-overlap')).toBe('false');
    });

    await test.step('expand the side panel and retain its conversation and source URL', async () => {
        await page.getByRole('link', { name: 'Collapse Exie to side panel' }).click();
        await expect(page).toHaveURL(/\/next\/stack(?:[?#]|$)/);
        await expect(page.locator('[data-assistant-panel]')).toBeVisible();
        await page.getByRole('button', { name: 'Close Exie' }).click();

        await page.goto('/next/stack?filter=status%3Aopen');
        await page.getByRole('button', { name: 'Open Exie' }).click();
        await page.getByRole('textbox', { name: 'Message Exie' }).fill('Keep this conversation when I expand it.');
        await page.getByRole('button', { name: 'Send message' }).click();
        await expect(page.getByText('This conversation followed you into the full-page chat.')).toBeVisible();

        await page.getByRole('link', { name: 'Expand Exie to full page' }).click();
        await expect(page).toHaveURL(/\/next\/exie\?/);
        const expandedUrl = new URL(page.url());
        expect(expandedUrl.searchParams.get('from')).toBe('/next/stack?filter=status%3Aopen');

        const fullPageChat = page.locator('[data-assistant-page]');
        await expect(fullPageChat).toBeVisible();
        await expect(fullPageChat.getByText('This conversation followed you into the full-page chat.')).toBeVisible();
        await expect(fullPageChat.getByRole('button', { name: 'Clear conversation' })).toBeEnabled();

        await expect.poll(async () => (await fullPageChat.boundingBox())?.height ?? 0).toBeGreaterThan(500);
        const fullPageBox = await fullPageChat.boundingBox();
        const contentAreaBox = await page.locator('main').boundingBox();
        const contentScrollerBox = await page.locator('main').locator('..').boundingBox();
        expect(fullPageBox).not.toBeNull();
        expect(contentAreaBox).not.toBeNull();
        expect(contentScrollerBox).not.toBeNull();
        expect(fullPageBox!.width).toBeGreaterThan(700);
        expect(fullPageBox!.x).toBeCloseTo(contentAreaBox!.x, 1);
        expect(fullPageBox!.width).toBeCloseTo(contentAreaBox!.width, 1);
        expect(fullPageBox!.x + fullPageBox!.width).toBeCloseTo(contentScrollerBox!.x + contentScrollerBox!.width, 1);
        expect(await fullPageChat.evaluate((element) => getComputedStyle(element).borderTopWidth)).toBe('0px');
    });
});
