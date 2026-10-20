import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import DateRangePicker from './date-range-picker.svelte';

function getCalendarDay(day: number): HTMLElement {
    const match = Array.from(document.querySelectorAll<HTMLElement>('[data-bits-day]:not([data-outside-month])')).find(
        (element) => element.textContent?.trim() === String(day)
    );
    if (!match) {
        throw new Error(`Calendar day ${day} was not rendered`);
    }

    return match;
}

describe('DateRangePicker', () => {
    it('previews a whole month and applies the original expression unchanged', async () => {
        const onselect = vi.fn();
        const view = render(DateRangePicker, { onselect, value: '[2025-01 TO 2025-01]' });

        await fireEvent.click(screen.getByRole('tab', { name: 'Relative' }));
        expect(screen.getByRole('textbox', { name: 'From' })).toHaveValue('2025-01');
        expect(screen.getByRole('textbox', { name: 'To' })).toHaveValue('2025-01');
        expect(document.querySelector('[id$="-start-status"]')?.textContent).toContain('Jan 1, 2025, 12:00:00 AM');
        expect(document.querySelector('[id$="-end-status"]')?.textContent).toContain('Jan 31, 2025, 11:59:59 PM');
        expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled();

        await fireEvent.click(screen.getByRole('button', { name: 'Apply' }));
        expect(onselect).toHaveBeenCalledExactlyOnceWith('[2025-01 TO 2025-01]');

        view.unmount();
        render(DateRangePicker, { value: '[2025-01 TO 2025-01]' });
        await fireEvent.click(screen.getByRole('tab', { name: 'Relative' }));
        expect(screen.getByRole('textbox', { name: 'From' })).toHaveValue('2025-01');
        expect(screen.getByRole('textbox', { name: 'To' })).toHaveValue('2025-01');
    });

    it('applies whole days picked on the calendar as calendar dates', async () => {
        const onselect = vi.fn();
        render(DateRangePicker, { onselect, value: '[2025-01-10 TO 2025-01-12]' });

        await fireEvent.click(getCalendarDay(15));
        await fireEvent.click(getCalendarDay(20));
        await fireEvent.click(screen.getByRole('button', { name: 'Apply' }));

        expect(onselect).toHaveBeenLastCalledWith('[2025-01-15 TO 2025-01-20]');

        await fireEvent.click(getCalendarDay(22));
        await fireEvent.click(screen.getByRole('button', { name: 'Apply' }));

        expect(onselect).toHaveBeenLastCalledWith('[2025-01-22 TO 2025-01-22]');
    });

    it('includes the time when an absolute bound is not a whole day', async () => {
        const onselect = vi.fn();
        render(DateRangePicker, { onselect, value: '[2025-01-10 TO 2025-01-12]' });

        const [startHour] = Array.from(document.querySelectorAll<HTMLElement>('[data-segment="hour"]'));
        startHour!.focus();
        await fireEvent.keyDown(startHour!, { key: 'ArrowUp' });
        await fireEvent.click(screen.getByRole('button', { name: 'Apply' }));

        expect(onselect).toHaveBeenLastCalledWith('[2025-01-10T01:00 TO 2025-01-12]');
    });

    it('allows applying a relative range after selecting the last 90 days', async () => {
        const onselect = vi.fn();
        render(DateRangePicker, {
            onselect,
            value: '[now-30d TO now]'
        });

        await fireEvent.click(screen.getByRole('button', { name: 'Last 90 days' }));
        expect(onselect).toHaveBeenLastCalledWith('[now-90d TO now]');
        expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument();

        const customRange = screen.getByRole('button', { name: 'Custom range' });
        expect(customRange).toHaveAttribute('aria-expanded', 'false');
        await fireEvent.click(customRange);
        expect(customRange).toHaveAttribute('aria-expanded', 'true');
        await fireEvent.click(screen.getByRole('tab', { name: 'Relative' }));

        const startInput = screen.getByRole('textbox', { name: 'From' });
        const endInput = screen.getByRole('textbox', { name: 'To' });
        await fireEvent.input(startInput, { target: { value: 'now-1y' } });
        await fireEvent.input(endInput, { target: { value: 'now' } });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await waitFor(() => expect((applyButton as HTMLButtonElement).disabled).toBe(false));
        await fireEvent.click(applyButton);

        expect(onselect).toHaveBeenLastCalledWith('[now-1y TO now]');
    });

    it('keeps a typed relative range when the absolute tab is only viewed before closing', async () => {
        const onselect = vi.fn();
        const { component } = render(DateRangePicker, { onselect, value: '[now-2h/h TO now]' });

        await fireEvent.input(screen.getByRole('textbox', { name: 'From' }), { target: { value: 'now-3h' } });
        await fireEvent.click(screen.getByRole('tab', { name: 'Absolute' }));
        component.apply();

        expect(onselect).toHaveBeenLastCalledWith('[now-3h TO now]');
    });

    it('opens custom relative ranges in the relative editor after the picker is remounted', async () => {
        const onselect = vi.fn();
        const initialRender = render(DateRangePicker, { onselect, value: '[now-30d TO now]' });

        await fireEvent.click(screen.getByRole('button', { name: 'Last 90 days' }));
        expect(onselect).toHaveBeenLastCalledWith('[now-90d TO now]');
        initialRender.unmount();

        render(DateRangePicker, { onselect, value: '[now-2h/h TO now]' });

        const startInput = screen.getByRole('textbox', { name: 'From' });
        const endInput = screen.getByRole('textbox', { name: 'To' });
        expect((startInput as HTMLInputElement).value).toBe('now-2h/h');
        expect((endInput as HTMLInputElement).value).toBe('now');
        await fireEvent.input(startInput, { target: { value: 'now-1y' } });

        const applyButton = screen.getByRole('button', { name: 'Apply' });
        await waitFor(() => expect((applyButton as HTMLButtonElement).disabled).toBe(false));
        await fireEvent.click(applyButton);

        expect(onselect).toHaveBeenLastCalledWith('[now-1y TO now]');
    });
});
