import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('./faceted-filter-actions.svelte', () => ({ default: null }));

import FacetedFilterNumber from './faceted-filter-number.svelte';

describe('FacetedFilterNumber', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it('cancels a pending valid value when the input becomes invalid', async () => {
        vi.useFakeTimers();
        const changed = vi.fn();
        render(FacetedFilterNumber, {
            changed,
            open: true,
            remove: vi.fn(),
            title: 'Response time'
        });

        const input = screen.getByLabelText('Filter by Response time');
        await fireEvent.input(input, { target: { value: '1' } });
        await vi.advanceTimersByTimeAsync(100);
        await fireEvent.input(input, { target: { value: '1e' } });
        await vi.advanceTimersByTimeAsync(500);

        expect(changed).not.toHaveBeenCalled();
    });
    it('cancels a pending valid value when the filter is cleared', async () => {
        vi.useFakeTimers();
        const changed = vi.fn();
        const { component } = render(FacetedFilterNumber, {
            changed,
            open: true,
            remove: vi.fn(),
            title: 'Response time'
        });

        const input = screen.getByLabelText('Filter by Response time');
        await fireEvent.input(input, { target: { value: '1' } });
        component.onClearFilter();
        await vi.advanceTimersByTimeAsync(500);

        expect(changed).not.toHaveBeenCalled();
    });
});
