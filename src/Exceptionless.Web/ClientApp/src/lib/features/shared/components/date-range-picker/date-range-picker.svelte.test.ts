import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import DateRangePicker from './date-range-picker.svelte';

describe('DateRangePicker', () => {
    it('previews a whole month and applies the original expression unchanged', async () => {
        const onselect = vi.fn();
        const view = render(DateRangePicker, { onselect, value: '[2025-01 TO 2025-01]' });

        expect(screen.getByRole('textbox', { name: 'Start' })).toHaveValue('2025-01');
        expect(screen.getByRole('textbox', { name: 'End' })).toHaveValue('2025-01');
        expect(document.querySelector('[id$="-start-status"]')?.textContent).toContain('Jan 1, 2025, 12:00:00 AM');
        expect(document.querySelector('[id$="-end-status"]')?.textContent).toContain('Jan 31, 2025, 11:59:59 PM');
        expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled();

        await fireEvent.click(screen.getByRole('button', { name: 'Apply' }));
        expect(onselect).toHaveBeenCalledExactlyOnceWith('[2025-01 TO 2025-01]');

        view.unmount();
        render(DateRangePicker, { value: '[2025-01 TO 2025-01]' });
        expect(screen.getByRole('textbox', { name: 'Start' })).toHaveValue('2025-01');
        expect(screen.getByRole('textbox', { name: 'End' })).toHaveValue('2025-01');
    });

    it('allows applying a custom range after selecting the last 90 days', async () => {
        const onselect = vi.fn();
        render(DateRangePicker, {
            onselect,
            value: '[now-30d TO now]'
        });

        await fireEvent.click(screen.getByRole('button', { name: 'Last 90 days' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Custom range' }));

        const startInput = screen.getByRole('textbox', { name: 'Start' });
        const endInput = screen.getByRole('textbox', { name: 'End' });
        await fireEvent.input(startInput, { target: { value: 'now-1y' } });
        await fireEvent.input(endInput, { target: { value: 'now' } });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await waitFor(() => expect((applyButton as HTMLButtonElement).disabled).toBe(false));
        await fireEvent.click(applyButton);

        expect(onselect).toHaveBeenLastCalledWith('[now-1y TO now]');
    });

    it('initializes a persisted common range after the picker is remounted', async () => {
        const onselect = vi.fn();
        const initialRender = render(DateRangePicker, { onselect, value: '[now-30d TO now]' });

        await fireEvent.click(screen.getByRole('button', { name: 'Last 90 days' }));
        expect(onselect).toHaveBeenLastCalledWith('[now-90d TO now]');
        initialRender.unmount();

        render(DateRangePicker, { onselect, value: '[now-90d TO now]' });
        await fireEvent.click(screen.getByRole('button', { name: 'Custom range' }));

        const startInput = screen.getByRole('textbox', { name: 'Start' });
        const endInput = screen.getByRole('textbox', { name: 'End' });
        expect((startInput as HTMLInputElement).value).toBe('now-90d');
        expect((endInput as HTMLInputElement).value).toBe('now');
        await fireEvent.input(startInput, { target: { value: 'now-1y' } });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await waitFor(() => expect((applyButton as HTMLButtonElement).disabled).toBe(false));
        await fireEvent.click(applyButton);

        expect(onselect).toHaveBeenLastCalledWith('[now-1y TO now]');
    });
});
