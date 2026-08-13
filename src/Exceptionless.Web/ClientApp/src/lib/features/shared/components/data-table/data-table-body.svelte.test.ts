import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import DataTableBodyTestHarness from './data-table-body.test-harness.svelte';

describe('DataTableBody', () => {
    it('keeps the row selection column at a fixed width', () => {
        render(DataTableBodyTestHarness, { kind: 'event', onRowClick: vi.fn() });

        const table = screen.getByRole('table');
        const selectHeader = screen.getByRole('columnheader', { name: 'Select' });
        const selectCell = screen.getByText('Select row').closest('td');

        expect(table.classList.contains('table-fixed')).toBe(true);
        expect(selectHeader.style.cssText).toBe('width: 32px; min-width: 32px; max-width: 32px;');
        expect(selectCell?.style.cssText).toBe('width: 32px; min-width: 32px; max-width: 32px;');
    });

    it('preserves every explicitly sized data column', () => {
        render(DataTableBodyTestHarness, { allColumnsSized: true, kind: 'event', onRowClick: vi.fn() });

        const table = screen.getByRole('table');
        const summaryHeader = screen.getByRole('columnheader', { name: 'Summary' });
        const dateHeader = screen.getByRole('columnheader', { name: 'Date' });

        expect(table.style.cssText).toBe('width: 302px; min-width: 302px;');
        expect(summaryHeader.style.cssText).toBe('width: 140px; min-width: 140px; max-width: 140px;');
        expect(dateHeader.style.cssText).toBe('width: 130px; min-width: 130px; max-width: 130px;');
    });

    it('uses the full-width data column as the flexible column', () => {
        render(DataTableBodyTestHarness, { fullWidthSummary: true, kind: 'event', onRowClick: vi.fn() });

        const summaryHeader = screen.getByRole('columnheader', { name: 'Summary' });
        const dateHeader = screen.getByRole('columnheader', { name: 'Date' });

        expect(summaryHeader.style.cssText).toBe('width: 100%;');
        expect(dateHeader.style.cssText).toBe('width: 150px; min-width: 150px; max-width: 150px;');
    });

    it('does not transfer flexibility after the full-width column is sized', () => {
        render(DataTableBodyTestHarness, { fullWidthSummary: true, kind: 'event', onRowClick: vi.fn(), sizedFullWidthSummary: true });

        const table = screen.getByRole('table');
        const summaryHeader = screen.getByRole('columnheader', { name: 'Summary' });
        const dateHeader = screen.getByRole('columnheader', { name: 'Date' });

        expect(table.style.cssText).toBe('width: 362px; min-width: 362px;');
        expect(summaryHeader.style.cssText).toBe('width: 180px; min-width: 180px; max-width: 180px;');
        expect(dateHeader.style.cssText).toBe('width: 150px; min-width: 150px; max-width: 150px;');
    });

    it('resizes a flexible column from its rendered width', async () => {
        render(DataTableBodyTestHarness, { fullWidthSummary: true, kind: 'event', onRowClick: vi.fn() });

        const summaryHeader = screen.getByRole('columnheader', { name: 'Summary' });
        vi.spyOn(summaryHeader, 'getBoundingClientRect').mockReturnValue({ width: 300 } as DOMRect);

        await fireEvent.keyDown(screen.getByRole('button', { name: 'Resize summary column' }), { key: 'ArrowRight' });

        expect(summaryHeader.style.cssText).toBe('width: 316px; min-width: 316px; max-width: 316px;');
    });

    it('lets a resized header shrink below its metadata width', () => {
        render(DataTableBodyTestHarness, { kind: 'event', onRowClick: vi.fn() });

        const header = screen.getByRole('columnheader', { name: 'Summary' });
        const headerContent = screen.getByText('Summary');

        expect(header.style.cssText).toContain('width: 160px');
        expect(header.style.cssText).toContain('min-width: 160px');
        expect(header.style.cssText).toContain('max-width: 160px');
        expect([...header.classList]).toEqual(expect.arrayContaining(['w-60', 'min-w-60', 'max-w-60']));
        expect(headerContent.classList.contains('w-60')).toBe(false);
        expect(headerContent.classList.contains('min-w-60')).toBe(false);
        expect(headerContent.classList.contains('max-w-60')).toBe(false);
    });

    it('opens event details for normal clicks anywhere in an event summary', async () => {
        const onRowClick = vi.fn();
        render(DataTableBodyTestHarness, { kind: 'event', onRowClick });

        await fireEvent.click(screen.getByText('IOException'));
        await fireEvent.click(screen.getByText('Unexpected end of Stream, the content may have already been read by another component.'));

        expect(onRowClick).toHaveBeenCalledTimes(2);
        expect(onRowClick).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'event-id' }), expect.any(MouseEvent));
    });

    it('uses that event details URL for middle and modified clicks', () => {
        render(DataTableBodyTestHarness, { kind: 'event', onRowClick: vi.fn() });

        const messageLink = screen.getByText('Unexpected end of Stream, the content may have already been read by another component.').closest('a');

        expect(messageLink?.getAttribute('href')).toBe('/next/event/event-id');
    });

    it('opens stack details for normal clicks anywhere in a stack summary', async () => {
        const onRowClick = vi.fn();
        render(DataTableBodyTestHarness, { kind: 'stack', onRowClick });

        await fireEvent.click(screen.getByText('IOException'));
        await fireEvent.click(screen.getByText('Unexpected end of Stream, the content may have already been read by another component.'));

        expect(onRowClick).toHaveBeenCalledTimes(2);
        expect(onRowClick).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'stack-id' }), expect.any(MouseEvent));
    });

    it('uses the stack details URL for middle and modified clicks', () => {
        render(DataTableBodyTestHarness, { kind: 'stack', onRowClick: vi.fn() });

        const messageLink = screen.getByText('Unexpected end of Stream, the content may have already been read by another component.').closest('a');

        expect(messageLink?.getAttribute('href')).toBe('/next/stack/stack-id');
    });
});
