import type { StockFeatures, TableOptions } from '@tanstack/svelte-table';
import { describe, expect, it } from 'vitest';

import { type QueryMeta, resolveConfiguredTableOptions, resolvePageCount, resolvePaginationChange } from './table.svelte';

describe('resolveConfiguredTableOptions', () => {
    it('preserves base reactive getters when configured options are returned from a spread', () => {
        // Arrange
        let rows = [{ id: 'one' }];
        const baseOptions = {
            _features: {},
            _rowModels: {},
            get columns() {
                return [];
            },
            get data() {
                return rows;
            }
        } as unknown as TableOptions<StockFeatures, { id: string }>;

        // Act
        const result = resolveConfiguredTableOptions(baseOptions, { ...baseOptions, manualSorting: false });
        rows = [{ id: 'two' }];

        // Assert
        expect(Object.getOwnPropertyDescriptor(result, 'data')?.get).toBeTypeOf('function');
        expect(result.data).toEqual([{ id: 'two' }]);
        expect(result.manualSorting).toBe(false);
    });
});

describe('resolvePaginationChange', () => {
    it('keeps the requested page when only the page index changes', () => {
        // Arrange
        const previousPageInfo = {
            pageIndex: 0,
            pageSize: 50
        };
        const requestedPageInfo = {
            pageIndex: 1,
            pageSize: 50
        };

        // Act
        const result = resolvePaginationChange(previousPageInfo, requestedPageInfo);

        // Assert
        expect(result).toEqual({
            currentPageInfo: requestedPageInfo,
            pageIndexChanged: false,
            pageSizeChanged: false,
            previousPageInfo
        });
    });

    it('resets to the first page when the page size changes from another page', () => {
        // Arrange
        const previousPageInfo = {
            pageIndex: 2,
            pageSize: 10
        };
        const requestedPageInfo = {
            pageIndex: 2,
            pageSize: 50
        };

        // Act
        const result = resolvePaginationChange(previousPageInfo, requestedPageInfo);

        // Assert
        expect(result).toEqual({
            currentPageInfo: {
                pageIndex: 0,
                pageSize: 50
            },
            pageIndexChanged: true,
            pageSizeChanged: true,
            previousPageInfo
        });
    });

    it('keeps the first page when changing page size from the first page', () => {
        // Arrange
        const previousPageInfo = {
            pageIndex: 0,
            pageSize: 10
        };
        const requestedPageInfo = {
            pageIndex: 0,
            pageSize: 50
        };

        // Act
        const result = resolvePaginationChange(previousPageInfo, requestedPageInfo);

        // Assert
        expect(result).toEqual({
            currentPageInfo: requestedPageInfo,
            pageIndexChanged: false,
            pageSizeChanged: true,
            previousPageInfo
        });
    });
});

describe('resolvePageCount', () => {
    it('uses the cursor total on the first page when the API returns one', () => {
        const meta = { links: { next: { after: 'next', rel: 'next', url: '/events?after=next' } }, total: 53000 } as QueryMeta;

        const result = resolvePageCount('cursor', meta, 1, 50, 0);

        expect(result).toBe(1060);
    });

    it('does not shrink a known cursor page count when a later page reports a smaller remaining total', () => {
        const meta = {
            links: {
                next: { after: 'next', rel: 'next', url: '/events?after=next' },
                previous: { before: 'previous', rel: 'previous', url: '/events?before=previous' }
            },
            total: 21200
        } as QueryMeta;

        const result = resolvePageCount('cursor', meta, 2, 50, 1060);

        expect(result).toBe(1060);
    });

    it('uses the current cursor page as the final page when there is no next link', () => {
        const meta = { links: { previous: { before: 'previous', rel: 'previous', url: '/events?before=previous' } }, total: 21200 } as QueryMeta;

        const result = resolvePageCount('cursor', meta, 20, 50, 1060);

        expect(result).toBe(20);
    });

    it('keeps offset pagination tied to the returned total', () => {
        const meta = { links: {}, total: 21200 } as QueryMeta;

        const result = resolvePageCount('offset', meta, 2, 50, 1060);

        expect(result).toBe(424);
    });

    it('keeps a known offset page count when a later response omits total but has a next page', () => {
        const meta = { links: { next: { page: '3', rel: 'next', url: '/events?page=3' } } } as QueryMeta;

        const result = resolvePageCount('offset', meta, 2, 50, 1060);

        expect(result).toBe(1060);
    });
});
