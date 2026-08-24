import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import DataTablePageSize from './data-table-page-size.svelte';
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

    it('joins bulk actions and pagination in one full-width sticky toolbar', () => {
        render(DataTablePagerTestHarness, { onPageIndexChange: vi.fn(), onPageSizeChange: vi.fn() });

        const toolbar = document.querySelector('[data-slot="data-table-footer"]');
        const pager = screen.getByRole('navigation', { name: 'Table pagination' });

        expect(screen.getByRole('toolbar', { name: 'Table controls' })).toBe(toolbar);
        expect(toolbar?.classList.contains('sticky')).toBe(true);
        expect(toolbar?.classList.contains('top-2')).toBe(true);
        expect(toolbar?.classList.contains('order-2')).toBe(false);
        expect(toolbar?.classList.contains('border')).toBe(true);
        expect(toolbar?.classList.contains('border-y')).toBe(false);
        expect(toolbar?.classList.contains('rounded-lg')).toBe(true);
        expect(toolbar?.classList.contains('gap-0')).toBe(true);
        expect(toolbar?.classList.contains('flex-wrap')).toBe(true);
        expect(toolbar?.classList.contains('sm:flex-nowrap')).toBe(true);
        expect(toolbar?.classList.contains('p-2')).toBe(false);
        expect(toolbar?.contains(screen.getByRole('button', { name: 'Bulk Actions' }))).toBe(true);
        expect(toolbar?.contains(pager)).toBe(true);
        expect(pager.classList.contains('max-sm:w-full')).toBe(true);
        expect(pager.classList.contains('max-sm:border-t')).toBe(true);

        const pageSize = screen.getByLabelText('Rows per page');
        expect(pageSize.getAttribute('data-size')).toBe('default');
        expect(pageSize.classList.contains('rounded-none')).toBe(true);
        expect(pageSize.classList.contains('border-y-0')).toBe(true);
        const pageLabel = screen.getByLabelText('Page 1 of 3');
        expect(pageLabel.classList.contains('select-none')).toBe(true);
        expect(pageLabel.classList.contains('rounded-none')).toBe(true);
        expect(pageLabel.classList.contains('border-y-0')).toBe(true);
        const nextButton = screen.getByRole('button', { name: 'Go to next page' });
        expect(nextButton.classList.contains('rounded-r-lg!')).toBe(true);
        expect(nextButton.classList.contains('rounded-l-none!')).toBe(true);
        expect(nextButton.classList.contains('border-r-0')).toBe(true);
    });

    it('keeps a standalone page-size selector fully bordered', () => {
        const table = { setPageSize: vi.fn() } as never;
        render(DataTablePageSize, { table, value: 10 });

        const pageSize = screen.getByLabelText('Rows per page');
        expect(pageSize.getAttribute('data-size')).toBe('sm');
        expect(pageSize.classList.contains('rounded-none')).toBe(false);
        expect(pageSize.classList.contains('border-y-0')).toBe(false);
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
