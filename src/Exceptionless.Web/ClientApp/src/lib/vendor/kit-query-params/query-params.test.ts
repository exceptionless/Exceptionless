import { afterEach, describe, expect, it, vi } from 'vitest';

import type { QueryParamValues, Schema } from './types';

import { createProxy } from './proxy';
import { applyQueryParamUpdates, debounce, parseURL, resetQueryParams } from './utils';

afterEach(() => {
    vi.useRealTimers();
});

describe('query parameter updates', () => {
    const schema = { date: 'date', filter: 'string', page: 'number' } satisfies Schema;

    it('does not change state or URL for unchanged values after coercion', () => {
        // Arrange
        const searchParams = new URLSearchParams('date=2026-08-09T00%3A00%3A00.000Z&page=1');
        const state = parseURL(searchParams, schema);
        const values = {
            date: new Date('2026-08-09T00:00:00Z'),
            page: '1'
        } as unknown as Partial<QueryParamValues<typeof schema>>;

        // Act
        const result = applyQueryParamUpdates(state, searchParams, values, schema);

        // Assert
        expect(result.stateChanged).toBe(false);
        expect(result.urlChanged).toBe(false);
        expect(result.searchParams.toString()).toBe(searchParams.toString());
    });

    it('does not serialize unchanged values supplied by defaults', () => {
        // Arrange
        const searchParams = new URLSearchParams('unknown=preserved');
        const defaults = { filter: 'all', page: 1 };
        const state = parseURL(searchParams, schema, defaults);

        // Act
        const result = applyQueryParamUpdates(state, searchParams, defaults, schema);

        // Assert
        expect(result.stateChanged).toBe(false);
        expect(result.urlChanged).toBe(false);
        expect(result.searchParams.toString()).toBe('unknown=preserved');
    });

    it('applies multiple values atomically while preserving unknown parameters', () => {
        // Arrange
        const searchParams = new URLSearchParams('filter=old&page=1&unknown=preserved');
        const state = parseURL(searchParams, schema);

        // Act
        const result = applyQueryParamUpdates(state, searchParams, { filter: 'new', page: 2 }, schema);

        // Assert
        expect(result.state).toEqual({ date: null, filter: 'new', page: 2 });
        expect(result.stateChanged).toBe(true);
        expect(result.urlChanged).toBe(true);
        expect(result.searchParams.toString()).toBe('filter=new&page=2&unknown=preserved');
        expect(searchParams.toString()).toBe('filter=old&page=1&unknown=preserved');
    });

    it('treats the literal null as an absent value', () => {
        // Arrange
        const searchParams = new URLSearchParams('filter=old&page=1');
        const state = parseURL(searchParams, schema);

        // Act
        const result = applyQueryParamUpdates(state, searchParams, { filter: 'null' }, schema);

        // Assert
        expect(result.state.filter).toBeNull();
        expect(result.searchParams.toString()).toBe('page=1');
    });

    it('resets schema values to defaults without removing unknown parameters', () => {
        // Arrange
        const searchParams = new URLSearchParams('filter=old&page=2&unknown=preserved');
        const state = parseURL(searchParams, schema);

        // Act
        const result = resetQueryParams(state, searchParams, schema, { filter: 'default', page: 1 });

        // Assert
        expect(result.state).toEqual({ date: null, filter: 'default', page: 1 });
        expect(result.searchParams.toString()).toBe('unknown=preserved');
        expect(result.stateChanged).toBe(true);
        expect(result.urlChanged).toBe(true);
    });
});

describe('query parameter proxy', () => {
    it('delegates property and batch updates to one update operation', () => {
        // Arrange
        const schema = { filter: 'string', page: 'number' } satisfies Schema;
        const state = { filter: null, page: 1 };
        const update = vi.fn();
        const reset = vi.fn();
        const toURLSearchParams = vi.fn(() => new URLSearchParams('page=1'));
        const queryParams = createProxy(state, schema, { reset, toURLSearchParams, update });

        // Act
        queryParams.page = 2;
        queryParams.update({ filter: 'new', page: 3 });
        queryParams.reset();
        const searchParams = queryParams.toURLSearchParams();

        // Assert
        expect(update).toHaveBeenNthCalledWith(1, { page: 2 });
        expect(update).toHaveBeenNthCalledWith(2, { filter: 'new', page: 3 });
        expect(reset).toHaveBeenCalledOnce();
        expect(searchParams.toString()).toBe('page=1');
    });
});

describe('query parameter URL synchronization', () => {
    it('runs immediately when debouncing is disabled', () => {
        // Arrange
        const synchronize = vi.fn();
        const synchronizeImmediately = debounce(synchronize, false);

        // Act
        synchronizeImmediately();

        // Assert
        expect(synchronize).toHaveBeenCalledOnce();
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
