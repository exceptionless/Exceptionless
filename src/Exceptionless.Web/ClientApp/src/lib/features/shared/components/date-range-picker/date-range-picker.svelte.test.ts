import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import DateRangePicker from './date-range-picker.svelte';

describe('DateRangePicker', () => {
    it('allows applying a custom range after selecting the last 90 days', async () => {
        const onselect = vi.fn();
        render(DateRangePicker, {
            onselect,
            value: '[now-30d TO now]'
        });

        await fireEvent.click(screen.getByRole('button', { name: 'Last 90 days' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Custom range' }));

        const startInput = screen.getByPlaceholderText('Start: now-1h, 2024-01-01');
        const endInput = screen.getByPlaceholderText('End: now, 2024-12-31');
        await fireEvent.input(startInput, { target: { value: 'now-1y' } });
        await fireEvent.input(endInput, { target: { value: 'now' } });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await waitFor(() => expect((applyButton as HTMLButtonElement).disabled).toBe(false));
        await fireEvent.click(applyButton);

        expect(onselect).toHaveBeenLastCalledWith('[now-1y TO now]');
    });

    it('allows editing a custom range after selecting the last 90 days while custom range is open', async () => {
        const onselect = vi.fn();
        render(DateRangePicker, {
            onselect,
            value: '[now-30d TO now]'
        });

        await fireEvent.click(screen.getByRole('button', { name: 'Custom range' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Last 90 days' }));

        const startInput = screen.getByPlaceholderText('Start: now-1h, 2024-01-01');
        const endInput = screen.getByPlaceholderText('End: now, 2024-12-31');
        expect((startInput as HTMLInputElement).value).toBe('now-90d');
        expect((endInput as HTMLInputElement).value).toBe('now');
        await fireEvent.input(startInput, { target: { value: 'now-1y' } });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await waitFor(() => expect((applyButton as HTMLButtonElement).disabled).toBe(false));
        await fireEvent.click(applyButton);

        expect(onselect).toHaveBeenLastCalledWith('[now-1y TO now]');
    });
});
