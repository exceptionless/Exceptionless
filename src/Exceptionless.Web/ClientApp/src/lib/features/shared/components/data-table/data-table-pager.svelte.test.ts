import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import DataTablePageSize from './data-table-page-size.svelte';
import DataTablePagerTestHarness from './data-table-pager.test-harness.svelte';

describe('DataTablePager', () => {
    const scrollIntoView = vi.fn();

    beforeEach(() => {
        Element.prototype.scrollIntoView = scrollIntoView;
    });

    afterEach(() => {
        cleanup();
        scrollIntoView.mockReset();
        vi.restoreAllMocks();
    });

    it('joins bulk actions and pagination in one full-width sticky toolbar', () => {
        render(DataTablePagerTestHarness, { onPageIndexChange: vi.fn(), onPageSizeChange: vi.fn(), variant: 'floating' });

        const toolbar = document.querySelector('[data-slot="data-table-footer"]');
        const pager = screen.getByRole('navigation', { name: 'Table pagination' });

        expect(screen.getByRole('toolbar', { name: 'Table controls' })).toBe(toolbar);
        expect(toolbar?.classList.contains('sticky')).toBe(true);
        expect(toolbar?.classList.contains('top-2')).toBe(true);
        expect(toolbar?.classList.contains('order-2')).toBe(false);
        expect(toolbar?.classList.contains('border')).toBe(true);
        expect(toolbar?.classList.contains('border-y')).toBe(false);
        expect(toolbar?.classList.contains('rounded-lg')).toBe(true);
        expect(toolbar?.classList.contains('border-border')).toBe(true);
        expect(toolbar?.getAttribute('data-floating')).toBeNull();
        expect(toolbar?.classList.contains('floating-glow')).toBe(false);
        expect(toolbar?.classList.contains('gap-0')).toBe(true);
        expect(toolbar?.classList.contains('flex-wrap')).toBe(true);
        expect(toolbar?.classList.contains('sm:flex-nowrap')).toBe(false);
        expect(toolbar?.classList.contains('p-2')).toBe(false);
        expect(toolbar?.contains(screen.getByRole('button', { name: 'Actions' }))).toBe(true);
        expect(toolbar?.contains(pager)).toBe(true);
        expect(pager.classList.contains('max-sm:w-full')).toBe(false);
        expect(pager.classList.contains('max-sm:border-t')).toBe(false);

        const pageSize = screen.getByLabelText('Rows per page');
        expect(pageSize.getAttribute('data-size')).toBe('default');
        expect(pageSize.classList.contains('min-w-14')).toBe(true);
        expect(pageSize.classList.contains('sm:min-w-18')).toBe(true);
        expect(pageSize.textContent).toContain('10 rows');
        expect(pageSize.classList.contains('rounded-none')).toBe(true);
        expect(pageSize.classList.contains('border-y-0')).toBe(true);
        const rowsLabel = Array.from(pageSize.querySelectorAll('span')).find((element) => element.textContent?.trim() === 'rows');
        expect(rowsLabel?.classList.contains('hidden')).toBe(true);
        expect(rowsLabel?.classList.contains('sm:inline')).toBe(true);
        const pageLabel = screen.getByLabelText('Page 1 of 3');
        expect(pageLabel.classList.contains('select-none')).toBe(true);
        expect(pageLabel.classList.contains('rounded-none')).toBe(true);
        expect(pageLabel.classList.contains('border-y-0')).toBe(true);
        const firstButton = screen.getByRole('button', { name: 'Go to first page' });
        expect(firstButton.getAttribute('aria-disabled')).toBe('true');
        expect(firstButton.classList.contains('rounded-none')).toBe(true);
        expect(firstButton.classList.contains('border-y-0')).toBe(true);
        const nextButton = screen.getByRole('button', { name: 'Go to next page' });
        expect(nextButton.classList.contains('rounded-r-lg!')).toBe(true);
        expect(nextButton.classList.contains('rounded-l-none!')).toBe(true);
        expect(nextButton.classList.contains('border-r-0')).toBe(true);
    });

    it('shows a simple standalone pager on the right by default', () => {
        render(DataTablePagerTestHarness, { onPageIndexChange: vi.fn(), onPageSizeChange: vi.fn() });

        const toolbar = document.querySelector('[data-slot="data-table-footer"]');
        const pager = screen.getByRole('navigation', { name: 'Table pagination' });

        expect(toolbar?.getAttribute('data-variant')).toBe('simple');
        expect(toolbar?.classList.contains('sticky')).toBe(false);
        expect(toolbar?.classList.contains('border')).toBe(false);
        expect(toolbar?.getAttribute('data-floating')).toBeNull();
        expect(toolbar?.classList.contains('floating-glow')).toBe(false);
        expect(toolbar?.classList.contains('justify-end')).toBe(true);
        expect(pager.getAttribute('data-variant')).toBe('simple');

        const pageSize = screen.getByLabelText('Rows per page');
        expect(pageSize.getAttribute('data-size')).toBe('default');
        expect(pageSize.classList.contains('rounded-none')).toBe(false);
        expect(pageSize.classList.contains('border-y-0')).toBe(false);
        expect(screen.getByLabelText('Page 1 of 3').classList.contains('border-y-0')).toBe(false);
        expect(screen.getByRole('button', { name: 'Go to next page' }).classList.contains('border-r-0')).toBe(false);
    });

    it('adds a neutral all-sided glow only after the sticky toolbar starts floating', async () => {
        render(DataTablePagerTestHarness, { onPageIndexChange: vi.fn(), onPageSizeChange: vi.fn(), variant: 'floating' });

        const toolbar = screen.getByRole('toolbar', { name: 'Table controls' });
        const scrollContainer = screen.getByTestId('scroll-container');
        const originalGetComputedStyle = window.getComputedStyle.bind(window);
        vi.spyOn(window, 'getComputedStyle').mockImplementation((element) => {
            if (element === toolbar) {
                return { position: 'sticky', top: '8px' } as CSSStyleDeclaration;
            }

            return originalGetComputedStyle(element);
        });
        vi.spyOn(toolbar, 'getBoundingClientRect').mockReturnValue({
            bottom: 40,
            height: 32,
            left: 0,
            right: 100,
            toJSON: () => ({}),
            top: 8,
            width: 100,
            x: 0,
            y: 8
        });

        scrollContainer.dispatchEvent(new Event('scroll'));

        await waitFor(() => expect(toolbar.getAttribute('data-floating')).toBe(''));
        expect(toolbar.classList.contains('floating-glow')).toBe(true);
    });

    it('positions the first row beneath floating controls before changing pages', async () => {
        const onPageIndexChange = vi.fn();
        render(DataTablePagerTestHarness, { onPageIndexChange, onPageSizeChange: vi.fn(), variant: 'floating' });

        const scrollContainer = screen.getByTestId('scroll-container');
        const tableRoot = document.querySelector<HTMLElement>('[data-slot="data-table"]')!;
        const toolbar = screen.getByRole('toolbar', { name: 'Table controls' });
        const tableBody = document.querySelector<HTMLElement>('[data-slot="data-table-body"]')!;
        const firstRow = tableBody.querySelector<HTMLElement>('tbody > tr:not(.hidden)')!;
        toolbar.setAttribute('data-floating', '');
        const scrollTo = vi.fn();
        scrollContainer.scrollTop = 400;
        scrollContainer.scrollTo = scrollTo;
        vi.spyOn(scrollContainer, 'getBoundingClientRect').mockReturnValue(createRect({ top: 60 }));
        vi.spyOn(toolbar, 'getBoundingClientRect').mockReturnValue(createRect({ height: 32, top: 68 }));
        vi.spyOn(tableBody, 'getBoundingClientRect').mockReturnValue(createRect({ top: -100 }));
        vi.spyOn(firstRow, 'getBoundingClientRect').mockReturnValue(createRect({ bottom: -40, height: 40, top: -80 }));

        const originalGetComputedStyle = window.getComputedStyle.bind(window);
        vi.spyOn(window, 'getComputedStyle').mockImplementation((element) => {
            if (element === toolbar) {
                return { position: 'sticky', top: '8px' } as CSSStyleDeclaration;
            }

            if (element === tableRoot) {
                return { rowGap: '8px' } as CSSStyleDeclaration;
            }

            return originalGetComputedStyle(element);
        });

        const nextButton = screen.getByRole('button', { name: 'Go to next page' });
        nextButton.focus();
        await fireEvent.click(nextButton);

        expect(scrollTo).toHaveBeenCalledWith({ behavior: 'auto', top: 192 });
        expect(onPageIndexChange).toHaveBeenCalledWith(1);
        expect(document.activeElement).toBe(nextButton);
    });

    it('preserves the scroll position when the first row is already visible', async () => {
        const onPageIndexChange = vi.fn();
        render(DataTablePagerTestHarness, { onPageIndexChange, onPageSizeChange: vi.fn(), variant: 'floating' });

        const scrollContainer = screen.getByTestId('scroll-container');
        const tableRoot = document.querySelector<HTMLElement>('[data-slot="data-table"]')!;
        const toolbar = screen.getByRole('toolbar', { name: 'Table controls' });
        const tableBody = document.querySelector<HTMLElement>('[data-slot="data-table-body"]')!;
        const firstRow = tableBody.querySelector<HTMLElement>('tbody > tr:not(.hidden)')!;
        toolbar.setAttribute('data-floating', '');
        const scrollTo = vi.fn();
        scrollContainer.scrollTop = 100;
        scrollContainer.scrollTo = scrollTo;
        vi.spyOn(scrollContainer, 'getBoundingClientRect').mockReturnValue(createRect({ bottom: 260, height: 200, top: 60 }));
        vi.spyOn(toolbar, 'getBoundingClientRect').mockReturnValue(createRect({ bottom: 100, height: 32, top: 68 }));
        vi.spyOn(tableBody, 'getBoundingClientRect').mockReturnValue(createRect({ top: 108 }));
        vi.spyOn(firstRow, 'getBoundingClientRect').mockReturnValue(createRect({ bottom: 148, height: 40, top: 108 }));

        const originalGetComputedStyle = window.getComputedStyle.bind(window);
        vi.spyOn(window, 'getComputedStyle').mockImplementation((element) => {
            if (element === toolbar) {
                return { position: 'sticky', top: '8px' } as CSSStyleDeclaration;
            }

            if (element === tableRoot) {
                return { rowGap: '8px' } as CSSStyleDeclaration;
            }

            return originalGetComputedStyle(element);
        });

        const nextButton = screen.getByRole('button', { name: 'Go to next page' });
        nextButton.focus();
        await fireEvent.click(nextButton);

        expect(scrollTo).not.toHaveBeenCalled();
        expect(onPageIndexChange).toHaveBeenCalledWith(1);
        expect(document.activeElement).toBe(nextButton);
    });

    it('keeps a standalone page-size selector fully bordered', () => {
        const table = { setPageSize: vi.fn() } as never;
        render(DataTablePageSize, { table, value: 10 });

        const pageSize = screen.getByLabelText('Rows per page');
        expect(pageSize.getAttribute('data-size')).toBe('sm');
        expect(pageSize.classList.contains('rounded-none')).toBe(false);
        expect(pageSize.classList.contains('border-y-0')).toBe(false);
    });

    it('keeps focus without scrolling the table when changing pages', async () => {
        const onPageIndexChange = vi.fn();
        render(DataTablePagerTestHarness, { onPageIndexChange, onPageSizeChange: vi.fn() });

        const nextButton = await screen.findByRole('button', { name: 'Go to next page' });
        nextButton.focus();
        await fireEvent.click(nextButton);

        expect(onPageIndexChange).toHaveBeenCalledWith(1);
        expect(screen.getByLabelText('Page 2 of 3')).toBeTruthy();
        expect(scrollIntoView).not.toHaveBeenCalled();
        expect(document.activeElement).toBe(nextButton);
    });

    it('returns directly to the first page without moving the controls', async () => {
        const onPageIndexChange = vi.fn();
        render(DataTablePagerTestHarness, { onPageIndexChange, onPageSizeChange: vi.fn() });

        await fireEvent.click(screen.getByRole('button', { name: 'Go to next page' }));
        const firstButton = screen.getByRole('button', { name: 'Go to first page' });
        firstButton.focus();
        await fireEvent.click(firstButton);

        expect(onPageIndexChange).toHaveBeenLastCalledWith(0);
        expect(screen.getByLabelText('Page 1 of 3')).toBeTruthy();
        expect(firstButton.getAttribute('aria-disabled')).toBe('true');
        expect(document.activeElement).toBe(firstButton);
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

function createRect(overrides: Partial<DOMRect> = {}): DOMRect {
    return {
        bottom: 0,
        height: 0,
        left: 0,
        right: 0,
        toJSON: () => ({}),
        top: 0,
        width: 0,
        x: 0,
        y: 0,
        ...overrides
    };
}
