import type { FetchClientResponse } from '@exceptionless/fetchclient';

import {
    type ColumnDef,
    type ColumnOrderState,
    type ColumnSort,
    type ColumnVisibilityState,
    createCoreRowModel,
    type PaginationState,
    type RowData,
    type RowSelectionState,
    stockFeatures,
    type StockFeatures,
    type Table,
    type TableOptions,
    type Updater
} from '@tanstack/svelte-table';
import { PersistedState } from 'runed';

import { DEFAULT_LIMIT } from './api/api.svelte';

export type PaginationStrategy = 'cursor' | 'memory' | 'offset';
export type QueryMeta = FetchClientResponse<unknown>['meta'];

export interface TableConfiguration<TData extends RowData, TPaginationStrategy extends PaginationStrategy = PaginationStrategy> {
    columnPersistenceKey: string;
    columns: ColumnDef<StockFeatures, TData, unknown>[];
    configureOptions?: (options: TableOptions<StockFeatures, TData>) => TableOptions<StockFeatures, TData>;
    defaultColumnOrder?: ColumnOrderState;
    defaultColumnVisibility?: ColumnVisibilityState;
    paginationStrategy: TPaginationStrategy;
    queryData?: TData[];
    queryMeta?: QueryMeta;
    queryParameters: TablePagingParameters<TPaginationStrategy>;
}

export interface TableCursorPagingParameters {
    after?: string;
    before?: string;
    limit?: number;
    page?: number;
    sort?: string;
}

export interface TableMemoryPagingParameters {
    limit?: number;
    page?: number;
}

export interface TableOffsetPagingParameters {
    limit?: number;
    page?: number;
    sort?: string;
}

export type TablePagingParameters<T extends PaginationStrategy = PaginationStrategy> = T extends 'cursor'
    ? TableCursorPagingParameters
    : T extends 'offset'
      ? TableOffsetPagingParameters
      : T extends 'memory'
        ? TableMemoryPagingParameters
        : never;

export function getSharedTableOptions<TData extends RowData, TPaginationStrategy extends PaginationStrategy = PaginationStrategy>(
    configuration: TableConfiguration<TData, TPaginationStrategy>
): TableOptions<StockFeatures, TData> {
    const isCursorPaging = $derived(!configuration.paginationStrategy || configuration.paginationStrategy === 'cursor');
    const isOffsetPaging = $derived(configuration.paginationStrategy === 'offset');
    const isMemoryPaging = $derived(configuration.paginationStrategy === 'memory');

    const [pageCount, setPageCount] = createTableState(0);
    const [columns, setColumns] = createTableState<ColumnDef<StockFeatures, TData, unknown>[]>(configuration.columns);

    $effect(() => {
        setColumns(configuration.columns);
    });

    // Use the persistKey if provided, otherwise default to events-column-visibility
    const visibilityKey = configuration.columnPersistenceKey ? `${configuration.columnPersistenceKey}-column-visibility` : 'events-column-visibility';
    const [persistedColumnVisibility, setColumnVisibility] = createPersistedTableState(
        visibilityKey,
        configuration.defaultColumnVisibility ?? <ColumnVisibilityState>{}
    );
    const columnVisibility = () => ({ ...configuration.defaultColumnVisibility, ...persistedColumnVisibility() });

    const orderKey = configuration.columnPersistenceKey ? `${configuration.columnPersistenceKey}-column-order` : 'events-column-order';
    const [persistedColumnOrder, setPersistedColumnOrder] = createPersistedTableState(orderKey, configuration.defaultColumnOrder ?? <ColumnOrderState>[]);
    const columnOrder = () => sanitizeColumnOrder(persistedColumnOrder(), columns());
    const setColumnOrder = (updaterOrValue: Updater<ColumnOrderState>) => {
        setPersistedColumnOrder(updaterOrValue instanceof Function ? updaterOrValue(resolveColumnOrder(columnOrder(), columns())) : updaterOrValue);
    };

    // Initialize pagination state from parameters
    const initialPageIndex = getPageIndexFromParameters(configuration.paginationStrategy, configuration.queryParameters, 0);

    const [pagination, setPagination] = createTableState<PaginationState>({
        pageIndex: initialPageIndex,
        pageSize: configuration.queryParameters.limit ?? DEFAULT_LIMIT
    });

    const [allData, setAllData] = createTableState<TData[]>([]);
    const [data, setData] = createTableState<TData[]>([]);
    const [meta, setMeta] = createTableState<QueryMeta | undefined>(undefined);
    const [sorting, setSorting] = createTableState<ColumnSort[]>(
        parseSortString(hasSortQueryParameter(configuration.queryParameters) ? configuration.queryParameters.sort : undefined)
    );
    const [rowSelection, setRowSelection] = createTableState<RowSelectionState>({});

    const onPaginationChange = (updaterOrValue: Updater<PaginationState>) => {
        const previousPageInfo = pagination();
        const requestedPageInfo = resolveUpdater(previousPageInfo, updaterOrValue);
        const paginationChange = resolvePaginationChange(previousPageInfo, requestedPageInfo);
        setPagination(paginationChange.currentPageInfo);

        const currentPageInfo = paginationChange.currentPageInfo;
        if (configuration.queryParameters.limit !== currentPageInfo.pageSize) {
            configuration.queryParameters.limit = currentPageInfo.pageSize;
        }

        // Handle memory pagination directly (no parameter change needed)
        if (isMemoryPaging && allData().length > 0) {
            const start = currentPageInfo.pageIndex * currentPageInfo.pageSize;
            setData(allData().slice(start, start + currentPageInfo.pageSize));
        } else if (isCursorPaging) {
            updateCursorPagingParameters(configuration.queryParameters as TableCursorPagingParameters, meta(), paginationChange);
        } else if (isOffsetPaging || isMemoryPaging) {
            updatePageNumberPagingParameters(configuration.queryParameters as TableMemoryPagingParameters | TableOffsetPagingParameters, currentPageInfo);
        }
    };

    const onSortingChange = (updaterOrValue: Updater<ColumnSort[]>) => {
        setSorting(updaterOrValue);
        const newSorting = sorting();

        if (isCursorPaging) {
            const parameters = configuration.queryParameters as TableCursorPagingParameters;
            parameters.after = undefined;
            parameters.before = undefined;
            if (hasSortQueryParameter(parameters)) {
                parameters.sort = serializeSortState(newSorting);
            }
        } else if (isOffsetPaging) {
            const parameters = configuration.queryParameters as TableOffsetPagingParameters;
            parameters.page = 1;
            if (hasSortQueryParameter(parameters)) {
                parameters.sort = serializeSortState(newSorting);
            }
        } else if (isMemoryPaging) {
            (configuration.queryParameters as TableMemoryPagingParameters).page = 1;
        }
    };

    const setDataImpl = (data: TData[]) => {
        if (isMemoryPaging) {
            setAllData(data);

            const pageInfo = pagination();
            const pageSize = pageInfo.pageSize;
            const maxValidPageIndex = Math.max(0, Math.ceil(data.length / pageSize) - 1);
            const needsAdjustment = pageInfo.pageIndex > maxValidPageIndex;
            const targetPageIndex = needsAdjustment ? maxValidPageIndex : pageInfo.pageIndex;

            // Update pagination state only if needed
            if (needsAdjustment) {
                setPagination((prev) => ({
                    ...prev,
                    pageIndex: targetPageIndex
                }));
            }

            // Calculate slice with the appropriate page index
            const start = targetPageIndex * pageSize;
            setData(data.slice(start, start + pageSize));
        } else {
            setData(data);
        }
    };

    const setMetaImpl = (meta: QueryMeta | undefined) => {
        setMeta(meta);
        const limit = configuration.queryParameters.limit ?? DEFAULT_LIMIT;
        const currentPage =
            (configuration.paginationStrategy === 'offset'
                ? (configuration.queryParameters as TableOffsetPagingParameters).page
                : (configuration.queryParameters as TableMemoryPagingParameters).page) ?? 1;
        const total = isMemoryPaging ? allData().length : (meta?.total as number | undefined);
        const totalPages = total != null ? Math.ceil(total / limit) : meta?.links?.next ? currentPage + 1 : currentPage;
        setPageCount(totalPages);

        // // Only adjust pagination for offset pagination here
        // // Memory pagination adjusts in setDataImpl to avoid duplication
        // if (isOffsetPaging) {
        //     const maxValidPageIndex = Math.max(0, totalPages - 1);
        //     const paginationState = pagination();
        //     if (paginationState.pageIndex > maxValidPageIndex) {
        //         setPagination((prev) => ({
        //             ...prev,
        //             pageIndex: maxValidPageIndex
        //         }));
        //     }
        // }
    };

    // NOTE: Two different effects are used here to avoid circular dependency issues with in memory paging.
    $effect(() => setDataImpl(configuration.queryData ?? []));
    $effect(() => setMetaImpl(configuration.queryMeta));
    $effect(() => {
        const nextPageSize = configuration.queryParameters.limit ?? DEFAULT_LIMIT;
        const nextPageIndex = getPageIndexFromParameters(configuration.paginationStrategy, configuration.queryParameters, pagination().pageIndex);
        const currentPageInfo = pagination();

        if (currentPageInfo.pageSize !== nextPageSize || currentPageInfo.pageIndex !== nextPageIndex) {
            setPagination({
                pageIndex: nextPageIndex,
                pageSize: nextPageSize
            });
        }
    });
    $effect(() => {
        if (!hasSortQueryParameter(configuration.queryParameters)) {
            return;
        }

        const parsedSort = parseSortString(configuration.queryParameters.sort);
        if (serializeSortState(parsedSort) !== serializeSortState(sorting())) {
            setSorting(parsedSort);
        }
    });

    const configureOptions = configuration.configureOptions ?? ((options) => options);
    return configureOptions({
        _features: stockFeatures,
        _rowModels: { coreRowModel: createCoreRowModel<StockFeatures, TData>() },
        get columns() {
            return columns();
        },
        set columns(value) {
            setColumns(value);
        },
        get data() {
            return data();
        },
        set data(value) {
            setDataImpl(value);
        },
        enableMultiRowSelection: true,
        enableRowSelection: true,
        enableSortingRemoval: false,
        getRowId: (originalRow) => {
            return originalRow && typeof originalRow === 'object' && 'id' in originalRow && originalRow.id != null
                ? String(originalRow.id)
                : JSON.stringify(originalRow);
        },
        manualPagination: true,
        manualSorting: true,
        get meta() {
            return meta();
        },
        set meta(value) {
            setMetaImpl(value);
        },
        onColumnOrderChange: setColumnOrder,
        onColumnVisibilityChange: setColumnVisibility,
        onPaginationChange,
        onRowSelectionChange: setRowSelection,
        onSortingChange,
        get pageCount() {
            return pageCount();
        },
        state: {
            get columnOrder() {
                return columnOrder();
            },
            get columnVisibility() {
                return columnVisibility();
            },
            get pagination() {
                return pagination();
            },
            get rowSelection() {
                return rowSelection();
            },
            get sorting() {
                return sorting();
            }
        }
    });
}

export function isTableEmpty<TData extends RowData>(table: Table<StockFeatures, TData>): boolean {
    return table.options.data.length === 0;
}

/***
 * Removes data from the table.
 * @param table The table to remove data from.
 * @param predicate A function that determines whether a row should be removed.
 * @returns True if data was removed, false otherwise.
 */
export function removeTableData<TData extends RowData>(
    table: Table<StockFeatures, TData>,
    predicate: (value: TData, index: number, array: TData[]) => boolean
): boolean {
    if ([...table.options.data].some(predicate)) {
        table.options.data = [...table.options.data].filter((value, index, array) => !predicate(value, index, array));

        return true;
    }

    return false;
}

/***
 * Removes a selection from the table.
 * @param table The table to remove the selection from.
 * @param selectionId The id of the selection to remove.
 * @returns True if the selection was removed, false otherwise.
 */
export function removeTableSelection<TData extends RowData>(table: Table<StockFeatures, TData>, selectionId: string): boolean {
    if (table.getIsSomeRowsSelected()) {
        const { rowSelection } = table.store.state;
        if (rowSelection[selectionId]) {
            table.setRowSelection((old) => {
                const filtered = Object.entries(old).filter(([id]) => id !== selectionId);
                return Object.fromEntries(filtered);
            });

            return true;
        }
    }

    return false;
}

export function resolvePaginationChange(previousPageInfo: PaginationState, currentPageInfo: PaginationState) {
    const pageSizeChanged = previousPageInfo.pageSize !== currentPageInfo.pageSize;
    if (!pageSizeChanged || currentPageInfo.pageIndex === 0) {
        return {
            currentPageInfo,
            pageIndexChanged: false,
            pageSizeChanged,
            previousPageInfo
        };
    }

    return {
        currentPageInfo: {
            ...currentPageInfo,
            pageIndex: 0
        },
        pageIndexChanged: true,
        pageSizeChanged,
        previousPageInfo
    };
}

function createPersistedTableState<T>(key: string, initialValue: T): [() => T, (updater: Updater<T>) => void] {
    const persistedValue = new PersistedState<T>(key, initialValue);

    return [
        () => persistedValue.current,
        (updater: Updater<T>) => {
            if (updater instanceof Function) {
                persistedValue.current = updater(persistedValue.current);
            } else {
                persistedValue.current = updater;
            }
        }
    ];
}

function createTableState<T>(initialValue: T): [() => T, (updater: Updater<T>) => void] {
    let value = $state(initialValue);

    return [
        () => value,
        (updater: Updater<T>) => {
            if (updater instanceof Function) {
                value = updater(value);
            } else {
                value = updater;
            }
        }
    ];
}

function getColumnIds<TData extends RowData>(columns: ColumnDef<StockFeatures, TData, unknown>[]): string[] {
    return columns.flatMap((column) => {
        const columnDefinition = column as { accessorKey?: number | string; columns?: ColumnDef<StockFeatures, TData, unknown>[]; id?: string };
        if (columnDefinition.columns) {
            return getColumnIds(columnDefinition.columns);
        }

        if (columnDefinition.id) {
            return [columnDefinition.id];
        }

        return typeof columnDefinition.accessorKey === 'string' ? [columnDefinition.accessorKey] : [];
    });
}

function getLinkQueryParameter(link: QueryMeta['links'][string] | undefined, name: string): string | undefined {
    const value = link?.[name];
    if (value) {
        return value;
    }

    if (!link?.url) {
        return undefined;
    }

    try {
        return new URL(link.url, 'https://example.com').searchParams.get(name) ?? undefined;
    } catch {
        return undefined;
    }
}

function getPageIndexFromParameters(strategy: PaginationStrategy, parameters: TablePagingParameters, fallbackPageIndex: number): number {
    if (strategy === 'cursor') {
        const cursorParameters = parameters as TableCursorPagingParameters;
        if (cursorParameters.page !== undefined) {
            return Math.max(0, cursorParameters.page - 1);
        }

        return cursorParameters.after || cursorParameters.before ? fallbackPageIndex : 0;
    }

    if (strategy !== 'offset' && strategy !== 'memory') {
        return fallbackPageIndex;
    }

    return Math.max(0, (((parameters as TableMemoryPagingParameters | TableOffsetPagingParameters).page ?? 1) as number) - 1);
}

function hasSortQueryParameter(parameters: TablePagingParameters): parameters is TableCursorPagingParameters | TableOffsetPagingParameters {
    return Object.prototype.hasOwnProperty.call(parameters, 'sort');
}

function parseSortString(sort: string | undefined): ColumnSort[] {
    if (!sort) {
        return [];
    }

    return sort
        .split(',')
        .map((value) => value.trim())
        .filter((value) => value.length > 0)
        .map((value) => ({
            desc: value.startsWith('-'),
            id: value.startsWith('-') ? value.slice(1) : value
        }))
        .filter((value) => value.id.length > 0);
}

function resolveColumnOrder<TData extends RowData>(columnOrder: ColumnOrderState, columns: ColumnDef<StockFeatures, TData, unknown>[]): ColumnOrderState {
    const defaultColumnOrder = getColumnIds(columns);
    const explicitColumnOrder = columnOrder.filter((columnId, index) => defaultColumnOrder.includes(columnId) && columnOrder.indexOf(columnId) === index);
    const nextColumnOrder = [...explicitColumnOrder, ...defaultColumnOrder.filter((columnId) => !explicitColumnOrder.includes(columnId))];

    return defaultColumnOrder.includes('select') ? ['select', ...nextColumnOrder.filter((columnId) => columnId !== 'select')] : nextColumnOrder;
}

function resolveUpdater<T>(currentValue: T, updaterOrValue: Updater<T>): T {
    if (updaterOrValue instanceof Function) {
        return updaterOrValue(currentValue);
    }

    return updaterOrValue;
}

function sanitizeColumnOrder<TData extends RowData>(columnOrder: ColumnOrderState, columns: ColumnDef<StockFeatures, TData, unknown>[]): ColumnOrderState {
    return columnOrder.length === 0 ? columnOrder : resolveColumnOrder(columnOrder, columns);
}

function serializeSortState(sorting: ColumnSort[]): string | undefined {
    return sorting.length > 0 ? sorting.map((sort) => `${sort.desc ? '-' : ''}${sort.id}`).join(',') : undefined;
}

function updateCursorPagingParameters(
    parameters: TableCursorPagingParameters,
    meta: QueryMeta | undefined,
    paginationChange: ReturnType<typeof resolvePaginationChange>
): void {
    const movingForward = paginationChange.currentPageInfo.pageIndex > paginationChange.previousPageInfo.pageIndex;
    const movingBackward = paginationChange.currentPageInfo.pageIndex < paginationChange.previousPageInfo.pageIndex;

    // Cursor tokens are only valid for the current page size and direction.
    // When the page size changes, clear both tokens and let the first page reload.
    parameters.after = !paginationChange.pageSizeChanged && movingForward ? getLinkQueryParameter(meta?.links?.next, 'after') : undefined;
    parameters.before =
        !paginationChange.pageSizeChanged && movingBackward && paginationChange.currentPageInfo.pageIndex > 0
            ? getLinkQueryParameter(meta?.links?.previous, 'before')
            : undefined;
    parameters.page = paginationChange.currentPageInfo.pageIndex > 0 ? paginationChange.currentPageInfo.pageIndex + 1 : undefined;
}

function updatePageNumberPagingParameters(parameters: TableMemoryPagingParameters | TableOffsetPagingParameters, currentPageInfo: PaginationState): void {
    parameters.page = currentPageInfo.pageIndex > 0 ? currentPageInfo.pageIndex + 1 : undefined; // API uses 1-based indexes.
}
