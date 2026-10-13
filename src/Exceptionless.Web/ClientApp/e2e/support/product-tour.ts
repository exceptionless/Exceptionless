import { expect, type Page } from '@playwright/test';

export async function expectActiveProductTour(page: Page, present: boolean): Promise<void> {
    const guide = page.getByRole('button', { exact: true, name: 'End guide' });
    if (present) {
        await expect(guide).toBeVisible();
    } else {
        await expect(guide).toBeHidden();
    }
}

export async function expectCalloutBesideTarget(page: Page): Promise<void> {
    await expect
        .poll(() =>
            page.evaluate(() => {
                const target = document.querySelector('.driver-active-element');
                const popover = document.querySelector<HTMLElement>('.driver-popover');
                const arrow = popover?.querySelector<HTMLElement>('.driver-popover-arrow');
                if (!target || !popover || !arrow) {
                    return false;
                }
                const targetBounds = target.getBoundingClientRect();
                const bounds = popover.getBoundingClientRect();
                const arrowBounds = arrow.getBoundingClientRect();
                const gap = Math.max(
                    targetBounds.left - bounds.right,
                    bounds.left - targetBounds.right,
                    targetBounds.top - bounds.bottom,
                    bounds.top - targetBounds.bottom
                );
                const verticalArrow = arrow.classList.contains('driver-popover-arrow-side-top') || arrow.classList.contains('driver-popover-arrow-side-bottom');
                const arrowCenter = verticalArrow ? arrowBounds.left + arrowBounds.width / 2 : arrowBounds.top + arrowBounds.height / 2;
                const targetStart = verticalArrow ? targetBounds.left : targetBounds.top;
                const targetEnd = verticalArrow ? targetBounds.right : targetBounds.bottom;
                return (
                    bounds.left >= 0 &&
                    bounds.top >= 0 &&
                    bounds.right <= innerWidth &&
                    bounds.bottom <= innerHeight &&
                    targetBounds.left >= 0 &&
                    targetBounds.top >= 0 &&
                    targetBounds.right <= innerWidth &&
                    targetBounds.bottom <= innerHeight &&
                    gap >= 0 &&
                    gap <= 24 &&
                    getComputedStyle(arrow).display !== 'none' &&
                    arrowCenter >= targetStart - 8 &&
                    arrowCenter <= targetEnd + 8
                );
            })
        )
        .toBe(true);
}

export async function mockAssistantAccess(page: Page): Promise<void> {
    await page.route(
        (url) => url.pathname === '/api/v2/assistant/access',
        (route) => route.fulfill({ json: { enabled: true, has_access: true, message: null, upgrade_required: false } })
    );
}

export async function startTourFromCommand(page: Page, title: string): Promise<void> {
    const announcementStart = page.getByRole('button', { name: 'See how it works' });
    if (title === 'Meet Exie' && (await announcementStart.isVisible())) {
        await announcementStart.click();
        return;
    }

    await page.getByRole('button', { name: 'Search Exceptionless' }).click();
    await page.getByRole('dialog').getByRole('option', { exact: true, name: 'Guided Tours' }).click();
    const catalog = page.getByRole('dialog', { name: 'Guided Tours' });
    const tour = catalog.getByRole('region', { name: title });
    await tour.getByRole('button', { name: /^(Continue|Restart|Start) / }).click();
}
