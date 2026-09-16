import { fireEvent, render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import Time from 'svelte-time';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import TimeAgo from './time-ago.svelte';

describe('TimeAgo', () => {
    beforeAll(() => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                disconnect() {}
                observe() {}
                unobserve() {}
            }
        );
    });

    afterAll(() => {
        vi.unstubAllGlobals();
    });

    afterEach(() => {
        vi.useRealTimers();
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

        expect(screen.getByText('an hour ago')).toBeTruthy();
    });

    it('exposes the full timestamp through an accessible tooltip', async () => {
        const value = new Date('2026-08-11T12:34:56Z');
        const { container } = render(TimeAgo, { value });

        const trigger = container.querySelector<HTMLElement>('[data-slot="tooltip-trigger"]');
        expect(trigger).not.toBeNull();
        await fireEvent.focus(trigger!);

        const tooltip = await screen.findByRole('tooltip');
        const expectedTimestamp = new Intl.DateTimeFormat(undefined, {
            day: 'numeric',
            hour: 'numeric',
            hour12: true,
            minute: '2-digit',
            month: 'short',
            second: '2-digit',
            timeZoneName: 'short',
            year: 'numeric'
        }).format(value);

        expect(tooltip.textContent).toContain(expectedTimestamp);
    });

    it('omits missing and invalid timestamps', () => {
        const missing = render(TimeAgo, { value: undefined });
        expect(missing.container.textContent).toBe('');
        expect(missing.container.querySelector('[data-slot="tooltip-trigger"]')).toBeNull();
        missing.unmount();

        const invalid = render(TimeAgo, { value: 'not a timestamp' });
        expect(invalid.container.textContent).toBe('');
        expect(invalid.container.querySelector('[data-slot="tooltip-trigger"]')).toBeNull();
    });
});
