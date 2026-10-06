import { render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import Time from 'svelte-time';
import { afterEach, describe, expect, it, vi } from 'vitest';

import TimeAgo from './time-ago.svelte';

describe('TimeAgo', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it.each([new Date(2026, 7, 11, 12, 34, 56), '2026-08-11T12:34:56'])('includes the full local timestamp in the native hover title', (value) => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date(2026, 7, 11, 12, 35, 56));
        const { container } = render(TimeAgo, { value });
        const time = container.querySelector('time');
        expect(time?.textContent).toContain('a minute ago');
        expect(time?.title).toBe(new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'long' }));
        expect(time?.querySelector('.sr-only')?.textContent).toContain(time!.title);
        expect(time?.hasAttribute('tabindex')).toBe(false);
    });

    it('keeps midnight and zero seconds in the hover title', () => {
        const { container } = render(TimeAgo, { value: new Date(2026, 0, 2, 0, 0, 0) });
        expect(container.querySelector('time')?.title).toBe(
            new Date(2026, 0, 2, 0, 0, 0).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'long' })
        );
    });

    it('updates the hover title when the timestamp changes', async () => {
        const { container, rerender } = render(TimeAgo, { value: new Date(2026, 0, 2, 0, 0, 0) });
        await rerender({ value: new Date(2026, 0, 3, 13, 4, 5) });
        expect(container.querySelector('time')?.title).toBe(
            new Date(2026, 0, 3, 13, 4, 5).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'long' })
        );
    });

    it('does not reformat the full timestamp when the relative clock ticks', async () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-08-11T12:35:56Z'));
        const format = vi.spyOn(Date.prototype, 'toLocaleString');
        try {
            render(TimeAgo, { value: '2026-08-11T12:34:56Z' });
            await tick();
            expect(format).toHaveBeenCalledExactlyOnceWith(undefined, { dateStyle: 'medium', timeStyle: 'long' });
            await vi.advanceTimersByTimeAsync(60_000);
            expect(format).toHaveBeenCalledTimes(1);
        } finally {
            format.mockRestore();
        }
    });

    it('does not loop when adaptive clocks straddle an age boundary', async () => {
        vi.useFakeTimers();
        const base = new Date('2026-08-11T12:00:00Z');
        vi.setSystemTime(base);

        render(Time, {
            live: true,
            relative: true,
            timestamp: new Date(base.getTime() - 2 * 60 * 60 * 1_000)
        });
        await tick();

        vi.setSystemTime(new Date(base.getTime() + 2_000));
        render(Time, {
            live: true,
            relative: true,
            timestamp: new Date(base.getTime() - 30 * 60 * 1_000)
        });
        await tick();

        render(TimeAgo, {
            value: new Date(base.getTime() - (60 * 60 * 1_000 - 1_000))
        });
        await tick();

        expect(screen.getByText(/an hour ago/)).toBeTruthy();
    });
});
