import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import DataTableBodyTestHarness from './data-table-body.test-harness.svelte';

describe('DataTableBody', () => {
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
