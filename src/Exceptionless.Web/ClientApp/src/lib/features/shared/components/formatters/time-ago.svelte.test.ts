import { render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import Time from 'svelte-time';
import { afterEach, describe, expect, it, vi } from 'vitest';

import TimeAgo from './time-ago.svelte';

describe('TimeAgo', () => {
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
});
