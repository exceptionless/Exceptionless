import type { FetchClientResponse } from '@exceptionless/fetchclient';

import {
    type ColumnDef,
    type ColumnSort,
    getCoreRowModel,
    type PaginationState,
    type RowSelectionState,
    type Table as SvelteTable,
    type TableOptions,
    type Updater,
    type VisibilityState
} from '@tanstack/svelte-table';
import { PersistedState } from 'runed';

import { DEFAULT_LIMIT } from './api/api.svelte';

export type PaginationStrategy = 'cursor' | 'memory' | 'offset';

export interface TableConfiguration<TData, TPaginationStrategy extends PaginationStrategy = PaginationStrategy> {
    columnPersistenceKey: string;
    columns: ColumnDef<TData>[];
    paginationStrategy: TPaginationStrategy;
    queryParameters: TablePagingParameters<TPaginationStrategy>;
    queryResponse?: FetchClientResponse<TData[]>;
}

export interface TableCursorPagingParameters {
    after?: string;
    before?: string;
    limit?: number;
    sort?: string;
}

export interface TableMemoryPagingParameters {
    limit?: number;
    sort?: string;
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

export function getSharedTableOptions<TData, TPaginationStrategy extends PaginationStrategy = PaginationStrategy>(
    configuration: TableConfiguration<TData, TPaginationStrategy>,
    configureOptions: (options: TableOptions<TData>) => TableOptions<TData> = (options) => options
): TableOptions<TData> {
    const paginationStrategy = $derived(configuration.paginationStrategy ?? 'cursor');
    const [pageCount, setPageCount] = createTableState(0);
    const [columns, setColumns] = createTableState<ColumnDef<TData>[]>(configuration.columns);

    // Use the persistKey if provided, otherwise default to events-column-visibility
    const visibilityKey = configuration.columnPersistenceKey ? `${configuration.columnPersistenceKey}-column-visibility` : 'events-column-visibility';
    const [columnVisibility, setColumnVisibility] = createPersistedTableState(visibilityKey, <VisibilityState>{});

    // Initialize pagination state from parameters
    const initialPageIndex =
        paginationStrategy === 'offset'
            ? (configuration.queryParameters as TableOffsetPagingParameters).page !== undefined
                ? Number((configuration.queryParameters as TableOffsetPagingParameters).page) - 1
                : 0
            : 0;

    const [pagination, setPagination] = createTableState<PaginationState>({
        pageIndex: initialPageIndex,
        pageSize: configuration.queryParameters.limit ?? DEFAULT_LIMIT
    });

    const [allData, setAllData] = createTableState<TData[]>([]);
    const [data, setData] = createTableState<TData[]>([]);
    const [meta, setMeta] = createTableState<FetchClientResponse<unknown>['meta'] | undefined>(undefined);
    const [sorting, setSorting] = createTableState<ColumnSort[]>([]);
    const [rowSelection, setRowSelection] = createTableState<RowSelectionState>({});

    const onPaginationChange = (updaterOrValue: Updater<PaginationState>) => {
        const previousPageIndex = pagination().pageIndex;

        setPagination(updaterOrValue);
        const currentPageInfo = pagination();

        configuration.queryParameters.limit = currentPageInfo.pageSize;

        // Handle memory pagination directly (no parameter change needed)
        if (paginationStrategy === 'memory' && allData().length > 0) {
            const start = currentPageInfo.pageIndex * currentPageInfo.pageSize;
            setData(allData().slice(start, start + currentPageInfo.pageSize));
        } else if (paginationStrategy === 'cursor') {
            const queryMeta = meta();
            const nextLink = queryMeta?.links?.next?.after;
            const previousLink = queryMeta?.links?.previous?.before;

            (configuration.queryParameters as TableCursorPagingParameters).after = currentPageInfo.pageIndex > previousPageIndex ? nextLink : undefined;
            // Ensure previousLink is only used when actually moving back and not on the first page
            (configuration.queryParameters as TableCursorPagingParameters).before =
                currentPageInfo.pageIndex < previousPageIndex && currentPageInfo.pageIndex > 0 ? previousLink : undefined;
        } else if (paginationStrategy === 'offset') {
            (configuration.queryParameters as TableOffsetPagingParameters).page = currentPageInfo.pageIndex + 1; // API uses 1-based index
        }
    };

    const onSortingChange = (updaterOrValue: Updater<ColumnSort[]>) => {
        setSorting(updaterOrValue);
        const newSorting = sorting();

        if (paginationStrategy === 'cursor') {
            (configuration.queryParameters as TableCursorPagingParameters).after = undefined;
            (configuration.queryParameters as TableCursorPagingParameters).before = undefined;
        } else if (paginationStrategy === 'offset') {
            (configuration.queryParameters as TableOffsetPagingParameters).page = 1;
        }

        configuration.queryParameters.sort = newSorting.length > 0 ? newSorting.map((sort) => `${sort.desc ? '-' : ''}${sort.id}`).join(',') : undefined;
    };

    const setDataImpl = (data: TData[]) => {
        if (paginationStrategy === 'memory') {
            setAllData(data);

            const pageInfo = pagination();
            const start = pageInfo.pageIndex * pageInfo.pageSize;
            setData(data.slice(start, start + pageInfo.pageSize));
        } else {
            setData(data);
        }
    };

    const setMetaImpl = (meta: FetchClientResponse<unknown>['meta'] | undefined) => {
        setMeta(meta);
        const limit = configuration.queryParameters.limit ?? DEFAULT_LIMIT;
        const total = (meta?.total as number) ?? 0;
        setPageCount(Math.ceil(total / limit));
    };

    $effect(() => {
        setDataImpl(configuration.queryResponse?.data ?? []);
        setMetaImpl(configuration.queryResponse?.meta);
    });

    return configureOptions({
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
        getCoreRowModel: getCoreRowModel(),
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
        onColumnVisibilityChange: setColumnVisibility,
        onPaginationChange,
        onRowSelectionChange: setRowSelection,
        onSortingChange,
        get pageCount() {
            return pageCount();
        },
        state: {
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

export function isTableEmpty<TData>(table: SvelteTable<TData>): boolean {
    return table.options.data.length === 0;
}

/***
 * Removes data from the table.
 * @param table The table to remove data from.
 * @param predicate A function that determines whether a row should be removed.
 * @returns True if data was removed, false otherwise.
 */
export function removeTableData<TData>(table: SvelteTable<TData>, predicate: (value: TData, index: number, array: TData[]) => boolean): boolean {
    if (table.options.data.some(predicate)) {
        table.options.data = table.options.data.filter((value, index, array) => !predicate(value, index, array));
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
export function removeTableSelection<TData>(table: SvelteTable<TData>, selectionId: string): boolean {
    if (table.getIsSomeRowsSelected()) {
        const { rowSelection } = table.getState();
        if (rowSelection[selectionId]) {
            table.setRowSelection((old: Record<string, boolean>) => {
                const filtered = Object.entries(old).filter(([id]) => id !== selectionId);
                return Object.fromEntries(filtered);
            });

            return true;
        }
    }

    return false;
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
