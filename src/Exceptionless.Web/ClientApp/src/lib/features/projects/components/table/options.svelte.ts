import type { FetchClientResponse } from '@exceptionless/fetchclient';

import NumberFormatter from '$comp/formatters/number.svelte';
import ProjectActionsCell from '$features/projects/components/table/project-actions-cell.svelte';
import { ViewProject } from '$features/projects/models';
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

import type { GetOrganizationProjectsParams, GetProjectsMode } from '../../api.svelte';

export function getColumns<ViewProject>(mode: GetProjectsMode = 'stats'): ColumnDef<ViewProject>[] {
    const columns: ColumnDef<ViewProject>[] = [
        {
            accessorKey: 'name',

            cell: (info) => info.getValue(),
            enableHiding: false,
            header: 'Name',
            meta: {
                class: 'w-[200px]'
            }
        }
    ];

    const isStatsMode = mode === 'stats';
    if (isStatsMode) {
        columns.push(
            {
                accessorKey: 'stack_count',
                cell: (info) => renderComponent(NumberFormatter, { value: info.getValue<number>() }),
                header: 'Stacks',
                meta: {
                    class: 'text-right w-24'
                }
            },
            {
                accessorKey: 'event_count',
                cell: (info) => renderComponent(NumberFormatter, { value: info.getValue<number>() }),
                header: 'Events',
                meta: {
                    class: 'text-right w-24'
                }
            }
        );
    }

    columns.push({
        cell: (info) => renderComponent(ProjectActionsCell, { project: info.row.original }),
        enableHiding: false,
        enableSorting: false,
        header: 'Actions',
        id: 'actions',
        meta: {
            class: 'w-32'
        }
    });

    return columns;
}

export function getTableContext<ViewProject>(
    params: GetOrganizationProjectsParams,
    configureOptions: (options: TableOptions<ViewProject>) => TableOptions<ViewProject> = (options) => options
) {
    let _parameters = $state(params);
    let _pageCount = $state(0);
    let _columns = $state(getColumns<ViewProject>(untrack(() => _parameters.mode)));
    let _data = $state([] as ViewProject[]);
    let _loading = $state(false);
    let _meta = $state({} as FetchClientResponse<unknown>['meta']);

    const [columnVisibility, setColumnVisibility] = createPersistedTableState('project-column-visibility', <VisibilityState>{});
    const [pagination, setPagination] = createTableState<PaginationState>({
        pageIndex: 0,
        pageSize: untrack(() => _parameters.limit) ?? DEFAULT_LIMIT
    });
    const [sorting, setSorting] = createTableState<ColumnSort[]>([
        {
            desc: true,
            id: 'name'
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
            page: undefined,
            sort:
                sorting().length > 0
                    ? sorting()
                          .map((sort) => `${sort.desc ? '-' : ''}${sort.id}`)
                          .join(',')
                    : undefined
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
