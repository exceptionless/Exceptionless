import { describe, expect, it } from 'vitest';

import { resolvePaginationChange } from './table.svelte';

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
