import { afterEach, describe, expect, expectTypeOf, it, vi } from 'vitest';

import type { QueryParameterInput, QueryParameterSchema } from './types';

import { createQueryParameterProxy } from './proxy';
import { applyQueryParameterUpdates, createDebouncedFunction, mergeQueryParameters, parseQueryParameters } from './query-params';

afterEach(() => {
    vi.useRealTimers();
});

describe('query parameter updates', () => {
    const schema = { date: 'date', filter: 'string', page: 'number' } satisfies QueryParameterSchema;

    it('does not change state or URL for unchanged values after coercion', () => {
        // Arrange
        const searchParams = new URLSearchParams('date=2026-08-09T00%3A00%3A00.000Z&page=1');
        const state = parseQueryParameters(searchParams, schema);
        const values = {
            date: new Date('2026-08-09T00:00:00Z'),
            page: '1'
        } as unknown as Partial<QueryParameterInput<typeof schema>>;

        // Act
        const result = applyQueryParameterUpdates(state, searchParams, values, schema);

        // Assert
        expect(result.stateChanged).toBe(false);
        expect(result.urlChanged).toBe(false);
        expect(result.searchParams.toString()).toBe(searchParams.toString());
    });

    it('does not serialize unchanged values supplied by defaults', () => {
        // Arrange
        const searchParams = new URLSearchParams('unknown=preserved');
        const defaults = { filter: 'all', page: 1 };
        const state = parseQueryParameters(searchParams, schema, defaults);

        // Act
        const result = applyQueryParameterUpdates(state, searchParams, defaults, schema);

        // Assert
        expect(result.stateChanged).toBe(false);
        expect(result.urlChanged).toBe(false);
        expect(result.searchParams.toString()).toBe('unknown=preserved');
    });

    it('applies multiple values atomically while preserving unknown parameters', () => {
        // Arrange
        const searchParams = new URLSearchParams('filter=old&page=1&unknown=preserved');
        const state = parseQueryParameters(searchParams, schema);

        // Act
        const result = applyQueryParameterUpdates(state, searchParams, { filter: 'new', page: 2 }, schema);

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
        const state = parseQueryParameters(searchParams, schema);

        // Act
        const result = applyQueryParameterUpdates(state, searchParams, { filter: 'null' }, schema);

        // Assert
        expect(result.state.filter).toBeNull();
        expect(result.searchParams.toString()).toBe('page=1');
    });

    it('preserves empty string markers only for string parameters', () => {
        // Arrange
        const searchParams = new URLSearchParams('filter=old&page=1');
        const state = parseQueryParameters(searchParams, schema);
        const values = { filter: '', page: '' } as unknown as Partial<QueryParameterInput<typeof schema>>;

        // Act
        const result = applyQueryParameterUpdates(state, searchParams, values, schema);
        const reparsed = parseQueryParameters(result.searchParams, schema);

        // Assert
        expect(result.state).toEqual({ date: null, filter: '', page: null });
        expect(result.searchParams.toString()).toBe('filter=');
        expect(reparsed).toEqual({ date: null, filter: '', page: null });
    });
});

describe('list query parameters', () => {
    const schema = { dates: 'date[]', pages: 'number[]', status: '<open,fixed>[]', tags: 'string[]' } satisfies QueryParameterSchema;

    it('parses repeated keys, dropping invalid and duplicate items, into frozen lists', () => {
        // Arrange
        const searchParams = new URLSearchParams(
            'status=open&status=bogus&status=fixed&status=open&pages=2&pages=x&pages=2&tags=a&tags=&tags=b&dates=2026-08-09T00:00:00Z&dates=2026-08-09T00:00:00.000Z'
        );

        // Act
        const state = parseQueryParameters(searchParams, schema);

        // Assert
        expect(state).toEqual({ dates: [new Date('2026-08-09T00:00:00Z')], pages: [2], status: ['open', 'fixed'], tags: ['a', 'b'] });
        expectTypeOf(state.status).toEqualTypeOf<readonly ('fixed' | 'open')[]>();
        expectTypeOf(state.dates).toEqualTypeOf<readonly Date[]>();
        expect(Object.values(state).every((value) => Object.isFrozen(value))).toBe(true);
        expect(() => (state.tags as string[]).push('c')).toThrow(TypeError);
    });

    it('writes lists as repeated keys and leaves the URL alone for equal lists', () => {
        // Arrange
        const searchParams = new URLSearchParams('dates=2026-08-09T00%3A00%3A00.000Z&unknown=preserved');
        const state = parseQueryParameters(searchParams, schema);

        // Act
        const unchanged = applyQueryParameterUpdates(state, searchParams, { dates: [new Date('2026-08-09T00:00:00Z')] }, schema);
        const changed = applyQueryParameterUpdates(state, searchParams, { pages: [1, 2], status: ['fixed', 'open'] }, schema);

        // Assert
        expect(unchanged.stateChanged).toBe(false);
        expect(unchanged.urlChanged).toBe(false);
        expect(changed.searchParams.toString()).toBe('dates=2026-08-09T00%3A00%3A00.000Z&unknown=preserved&pages=1&pages=2&status=fixed&status=open');
        expect(changed.state.status).toEqual(['fixed', 'open']);
        expect(Object.isFrozen(changed.state.status)).toBe(true);
    });

    it.each([
        { defaults: undefined, expectedUrl: 'unknown=preserved', label: 'removes the key without a default' },
        { defaults: { status: ['open'] as const }, expectedUrl: 'status=&unknown=preserved', label: 'writes an empty marker over a non-empty default' }
    ])('$label when a list is emptied', ({ defaults, expectedUrl }) => {
        // Arrange
        const searchParams = new URLSearchParams('status=fixed&unknown=preserved');
        const state = parseQueryParameters(searchParams, schema, defaults);

        // Act
        const result = applyQueryParameterUpdates(state, searchParams, { status: [] }, schema, defaults);
        const reparsed = parseQueryParameters(result.searchParams, schema, defaults);

        // Assert
        expect(result.searchParams.toString()).toBe(expectedUrl);
        expect(reparsed.status).toEqual([]);
    });

    it('applies only owned keys onto the live URL, keeping parameter order', () => {
        // Arrange
        const live = 'other=b&filter=old&tags=x&page=4';
        const own = new URLSearchParams('filter=new&page=1&stale=ignored');

        // Act
        const merged = mergeQueryParameters(live, own, { filter: 'string', tags: 'string[]' });

        // Assert
        expect(merged.toString()).toBe('other=b&filter=new&page=4');
    });
});

describe('query parameter proxy', () => {
    it('delegates supported assignments and ignores unsupported properties', () => {
        // Arrange
        const schema = { filter: 'string', page: 'number' } satisfies QueryParameterSchema;
        const state = { filter: null, page: 1 };
        const update = vi.fn();
        const queryParams = createQueryParameterProxy(state, schema, { update });

        // Act
        queryParams.page = 2;
        queryParams.update({ filter: 'new', page: 3 });
        (queryParams as typeof queryParams & { filters: null | string }).filters = null;

        // Assert
        expect(update).toHaveBeenNthCalledWith(1, { page: 2 });
        expect(update).toHaveBeenNthCalledWith(2, { filter: 'new', page: 3 });
        expect(update).toHaveBeenCalledTimes(2);
    });
});

describe('query parameter URL synchronization', () => {
    it('runs immediately when the debounce is zero', () => {
        // Arrange
        const synchronize = vi.fn();
        const synchronizeImmediately = createDebouncedFunction(synchronize, 0);

        // Act
        synchronizeImmediately();

        // Assert
        expect(synchronize).toHaveBeenCalledOnce();
    });

    it('cancels pending synchronization before it runs', async () => {
        // Arrange
        vi.useFakeTimers();
        const synchronize = vi.fn();
        const debouncedSynchronize = createDebouncedFunction(synchronize, 200);

        // Act
        debouncedSynchronize();
        debouncedSynchronize.cancel();
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(synchronize).not.toHaveBeenCalled();
    });
});
