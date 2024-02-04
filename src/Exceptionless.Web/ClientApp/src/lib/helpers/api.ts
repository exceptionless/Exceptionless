export const DEFAULT_LIMIT = 10;

export function getPageStart(page: number, limit: number): number {
    return page * limit + 1;
}

export function getPageEnd(page: number, pageTotal: number, limit: number): number {
    return getPageStart(page, limit) + pageTotal - 1;
}

export function canNavigateToFirstPage(page: number): boolean {
    return page > 1;
}

export function hasPreviousPage(page: number): boolean {
    return page > 0;
}

export function hasNextPage(page: number, pageTotal: number, limit: number, total: number): boolean {
    const end = getPageEnd(page, pageTotal, limit);
    return pageTotal === limit && end < total;
}
