import { afterEach, describe, expect, it, vi } from 'vitest';

import type { Schema } from './types';

import { createProxy } from './proxy';
import { clearSearchParamPaths, debounce, setSearchParamIfChanged } from './utils';

afterEach(() => {
    vi.useRealTimers();
});

describe('query parameter proxy', () => {
    it('does not publish unchanged primitive values after coercion', () => {
        // Arrange
        const onUpdate = vi.fn();
        const schema = { date: 'date', page: 'number' } satisfies Schema;
        const queryParams = createProxy(
            { date: new Date('2026-08-09T00:00:00Z'), page: 1 },
            {
                onUpdate,
                reset: vi.fn(),
                schema,
                searchParams: new URLSearchParams(),
                sync: vi.fn()
            }
        );

        // Act
        queryParams.page = 1;
        queryParams.date = new Date('2026-08-09T00:00:00Z');
        Reflect.set(queryParams, 'page', '1');

        // Assert
        expect(onUpdate).not.toHaveBeenCalled();
    });

    it('publishes a changed primitive value once', () => {
        // Arrange
        const onUpdate = vi.fn();
        const schema = { page: 'number' } satisfies Schema;
        const queryParams = createProxy(
            { page: 1 },
            {
                onUpdate,
                reset: vi.fn(),
                schema,
                searchParams: new URLSearchParams(),
                sync: vi.fn()
            }
        );

        // Act
        queryParams.page = 2;

        // Assert
        expect(onUpdate).toHaveBeenCalledOnce();
        expect(onUpdate).toHaveBeenCalledWith('page', '2');
    });
});

describe('query parameter URL synchronization', () => {
    it('changes search parameters only when their serialized value changes', () => {
        // Arrange
        const searchParams = new URLSearchParams('page=1&filter=value');

        // Act and assert
        expect(setSearchParamIfChanged(searchParams, 'page', '1')).toBe(false);
        expect(setSearchParamIfChanged(searchParams, 'missing', null)).toBe(false);
        expect(setSearchParamIfChanged(searchParams, 'page', '2')).toBe(true);
        expect(setSearchParamIfChanged(searchParams, 'filter', null)).toBe(true);
        expect(searchParams.toString()).toBe('page=2');
    });

    it('clears matching paths only when they exist', () => {
        // Arrange
        const searchParams = new URLSearchParams('filters.0=a&filters.1=b&page=1');

        // Act and assert
        expect(clearSearchParamPaths(searchParams, 'missing')).toBe(false);
        expect(clearSearchParamPaths(searchParams, 'filters')).toBe(true);
        expect(searchParams.toString()).toBe('page=1');
    });

    it('cancels pending synchronization before it runs', async () => {
        // Arrange
        vi.useFakeTimers();
        const synchronize = vi.fn();
        const debouncedSynchronize = debounce(synchronize, 200);

        // Act
        debouncedSynchronize();
        debouncedSynchronize.cancel();
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(synchronize).not.toHaveBeenCalled();
    });
});
