import type { FetchClientResponse } from '@exceptionless/fetchclient';

import WebhookActionsCell from '$features/webhooks/components/table/webhook-actions-cell.svelte';
import { Webhook } from '$features/webhooks/models';
import { DEFAULT_LIMIT } from '$shared/api/api.svelte';
import {
    type ColumnDef,
    type ColumnSort,
    getCoreRowModel,
    type PaginationState,
    renderComponent,
    type RowSelectionState,
    type TableOptions,
    type Updater,
    type VisibilityState
} from '@tanstack/svelte-table';
import { PersistedState } from 'runed';
import { untrack } from 'svelte';

import type { GetProjectWebhooksParams } from '../../api.svelte';

export function getColumns<TWebhook extends Webhook>(): ColumnDef<TWebhook>[] {
    const columns: ColumnDef<TWebhook>[] = [
        {
            accessorKey: 'url',
            cell: (info) => info.getValue(),
            enableHiding: false,
            enableSorting: false,
            header: 'Url',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            accessorKey: 'event_types',
            cell: (info) => info.getValue(),
            enableHiding: true,
            enableSorting: false,
            header: 'Event Types',
            meta: {
                class: 'w-[200px]'
            }
        },
        {
            cell: (info) => renderComponent(WebhookActionsCell, { webhook: info.row.original }),
            enableHiding: false,
            enableSorting: false,
            header: 'Actions',
            id: 'actions',
            meta: {
                class: 'w-16'
            }
        }
    ];

    return columns;
}

export function getTableContext<TWebhook extends Webhook>(
    params: GetProjectWebhooksParams,
    configureOptions: (options: TableOptions<TWebhook>) => TableOptions<TWebhook> = (options) => options
) {
    let _parameters = $state(params);
    let _pageCount = $state(0);
    let _columns = $state(getColumns<TWebhook>());
    let _data = $state([] as TWebhook[]);
    let _loading = $state(false);
    let _meta = $state({} as FetchClientResponse<unknown>['meta']);

    const [columnVisibility, setColumnVisibility] = createPersistedTableState('webhook-column-visibility', <VisibilityState>{});
    const [pagination, setPagination] = createTableState<PaginationState>({
        pageIndex: 0,
        pageSize: untrack(() => _parameters.limit) ?? DEFAULT_LIMIT
    });
    const [sorting, setSorting] = createTableState<ColumnSort[]>([
        {
            desc: true,
            id: 'url'
        }
    ]);
    const [rowSelection, setRowSelection] = createTableState<RowSelectionState>({});
    const onPaginationChange = (updaterOrValue: Updater<PaginationState>) => {
        if (_loading) {
            return;
        }

        _loading = true;
        setPagination(updaterOrValue);

        const currentPageInfo = pagination();
        const nextLink = _meta.links?.next?.after as string;
        const previousLink = _meta.links?.previous?.before as string;

        _parameters = {
            ..._parameters,
            limit: currentPageInfo.pageSize,
            page: !nextLink && !previousLink && currentPageInfo.pageIndex !== 0 ? currentPageInfo.pageIndex + 1 : undefined
        };
    };

    const onSortingChange = (updaterOrValue: Updater<ColumnSort[]>) => {
        setSorting(updaterOrValue);

        _parameters = {
            ..._parameters,
            page: undefined
        };
    };

    const options = configureOptions({
        get columns() {
            return _columns;
        },
        set columns(value) {
            _columns = value;
        },
        get data() {
            return _data;
        },
        set data(value) {
            _data = value;
        },
        enableMultiRowSelection: true,
        enableRowSelection: true,
        enableSortingRemoval: false,
        getCoreRowModel: getCoreRowModel(),
        getRowId: (originalRow) => originalRow.id,
        manualPagination: true,
        manualSorting: true,
        onColumnVisibilityChange: setColumnVisibility,
        onPaginationChange,
        onRowSelectionChange: setRowSelection,
        onSortingChange,
        get pageCount() {
            return _pageCount;
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

    return {
        get data() {
            return _data;
        },
        set data(value) {
            _data = value;
        },
        get loading() {
            return _loading;
        },
        get meta() {
            return _meta;
        },
        set meta(value) {
            _meta = value;

            const limit = _parameters.limit ?? DEFAULT_LIMIT;
            const total = (_meta?.total as number) ?? 0;
            _pageCount = Math.ceil(total / limit);

            _loading = false;
        },
        options,
        get pageCount() {
            return _pageCount;
        },
        get parameters() {
            return _parameters;
        }
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
