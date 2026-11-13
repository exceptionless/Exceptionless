import { flushSync, tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { clock, CLOCK_TICK_MS } from './clock.svelte';

describe('clock', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-11-11T12:00:00Z'));
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('updates readers each tick and stops its timer when nothing reads it', async () => {
        // Arrange
        const seen: number[] = [];
        const cleanup = $effect.root(() => {
            $effect(() => {
                seen.push(clock.now.getTime());
            });
        });
        flushSync();

        // Act
        vi.advanceTimersByTime(CLOCK_TICK_MS);
        flushSync();
        cleanup();
        await tick(); // Svelte stops an unused subscriber on a microtask.
        const timersAfterCleanup = vi.getTimerCount();

        // Assert
        expect(seen).toEqual([Date.parse('2026-11-11T12:00:00Z'), Date.parse('2026-11-11T12:01:00Z')]);
        expect(timersAfterCleanup).toBe(0);
    });

    it('updates readers when the tab becomes visible again', () => {
        // Arrange
        const seen: number[] = [];
        const cleanup = $effect.root(() => {
            $effect(() => {
                seen.push(clock.now.getTime());
            });
        });
        flushSync();
        vi.setSystemTime(new Date('2026-11-12T08:00:00Z'));

        // Act
        document.dispatchEvent(new Event('visibilitychange'));
        flushSync();
        cleanup();

        // Assert
        expect(seen.at(-1)).toBe(Date.parse('2026-11-12T08:00:00Z'));
    });
});
