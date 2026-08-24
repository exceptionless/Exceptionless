import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import DataTablePagerTestHarness from './data-table-pager.test-harness.svelte';

describe('DataTablePager', () => {
    const scrollIntoView = vi.fn();

    beforeEach(() => {
        Element.prototype.scrollIntoView = scrollIntoView;
        Object.defineProperty(window, 'matchMedia', {
            configurable: true,
            value: vi.fn().mockReturnValue({ matches: false })
        });
    });

    afterEach(() => {
        cleanup();
        scrollIntoView.mockReset();
    });

    it('keeps bulk actions and pagination together in the solid sticky table toolbar', () => {
        render(DataTablePagerTestHarness, { onPageIndexChange: vi.fn(), onPageSizeChange: vi.fn() });

        const toolbar = document.querySelector('[data-slot="data-table-footer"]');
        const pager = screen.getByRole('navigation', { name: 'Table pagination' });

        expect(toolbar?.classList.contains('sticky')).toBe(true);
        expect(toolbar?.classList.contains('top-2')).toBe(true);
        expect(toolbar?.classList.contains('order-2')).toBe(false);
        expect(toolbar?.classList.contains('border')).toBe(false);
        expect(toolbar?.classList.contains('border-y')).toBe(true);
        expect(toolbar?.classList.contains('bg-background')).toBe(true);
        expect(toolbar?.classList.contains('p-2')).toBe(false);
        expect(toolbar?.contains(screen.getByRole('button', { name: 'Bulk Actions' }))).toBe(true);
        expect(toolbar?.contains(pager)).toBe(true);
        expect(screen.getByLabelText('Page 1 of 3').classList.contains('select-none')).toBe(true);
    });

    it('keeps focus and scrolls the table to the start when changing pages', async () => {
        const onPageIndexChange = vi.fn();
        render(DataTablePagerTestHarness, { onPageIndexChange, onPageSizeChange: vi.fn() });

        const nextButton = await screen.findByRole('button', { name: 'Go to next page' });
        nextButton.focus();
        await fireEvent.click(nextButton);

        expect(onPageIndexChange).toHaveBeenCalledWith(1);
        expect(screen.getByLabelText('Page 2 of 3')).toBeTruthy();
        expect(scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' });
        expect(document.activeElement).toBe(nextButton);
    });

    it('keeps an unavailable paging target focused without changing pages', async () => {
        const onPageIndexChange = vi.fn();
        render(DataTablePagerTestHarness, { onPageIndexChange, onPageSizeChange: vi.fn() });

        const previousButton = screen.getByRole('button', { name: 'Go to previous page' });
        previousButton.focus();
        await fireEvent.click(previousButton);

        expect(previousButton.getAttribute('aria-disabled')).toBe('true');
        expect(onPageIndexChange).not.toHaveBeenCalled();
        expect(document.activeElement).toBe(previousButton);
    });
});
