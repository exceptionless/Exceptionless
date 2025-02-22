import type { StackStatus } from '$features/stacks/models';
import type { FetchClientResponse } from '@exceptionless/fetchclient';

import NumberFormatter from '$comp/formatters/number.svelte';
import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { nameof } from '$lib/utils';
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

import type { GetEventsMode, GetEventsParams } from '../../api.svelte';
import type { EventSummaryModel, StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '../summary/index';

import Summary from '../summary/summary.svelte';
import EventsUserIdentitySummaryCell from './events-user-identity-summary-cell.svelte';
import StackStatusCell from './stack-status-cell.svelte';
import StackUsersSummaryCell from './stack-users-summary-cell.svelte';

export function getColumns<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(mode: GetEventsMode = 'summary'): ColumnDef<TSummaryModel>[] {
    const columns: ColumnDef<TSummaryModel>[] = [
        {
            cell: (props) =>
                renderComponent(Checkbox, {
                    'aria-label': 'Select row',
                    checked: props.row.getIsSelected(),
                    class: 'translate-y-[2px]',
                    disabled: !props.row.getCanSelect(),
                    indeterminate: props.row.getIsSomeSelected(),
                    onCheckedChange: (checked: 'indeterminate' | boolean) => props.row.getToggleSelectedHandler()({ target: { checked } })
                }),
            enableHiding: false,
            enableSorting: false,
            header: ({ table }) =>
                renderComponent(Checkbox, {
                    checked: table.getIsAllRowsSelected(),
                    indeterminate: table.getIsSomeRowsSelected(),
                    onCheckedChange: (checked: 'indeterminate' | boolean) => table.getToggleAllRowsSelectedHandler()({ target: { checked } })
                }),
            id: 'select',
            meta: {
                class: 'w-6'
            }
        },
        {
            cell: (prop) => renderComponent(Summary, { summary: prop.row.original }),
            enableHiding: false,
            header: 'Summary'
        }
    ];

    const isEventSummary = mode === 'summary';
    if (isEventSummary) {
        columns.push(
            {
                cell: (prop) => renderComponent(EventsUserIdentitySummaryCell, { summary: prop.row.original }),
                enableSorting: false,
                header: 'User',
                id: 'user',
                meta: {
                    class: 'w-28'
                }
            },
            {
                accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('date'),
                cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() }),
                header: 'Date',
                id: 'date',
                meta: {
                    class: 'w-36'
                }
            }
        );
    } else {
        columns.push(
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('status'),
                cell: (prop) => renderComponent(StackStatusCell, { value: prop.getValue<StackStatus>() }),
                enableSorting: false,
                header: 'Status',
                id: 'status',
                meta: {
                    class: 'w-36'
                }
            },
            {
                cell: (prop) => renderComponent(StackUsersSummaryCell, { summary: prop.row.original }),
                enableSorting: false,
                header: 'Users',
                id: 'users',
                meta: {
                    class: 'w-24'
                }
            },
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('total'),
                cell: (prop) => renderComponent(NumberFormatter, { value: prop.getValue<number>() }),
                enableSorting: false,
                header: 'Events',
                id: 'events',
                meta: {
                    class: 'w-24'
                }
            },
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('first_occurrence'),
                cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() }),
                enableSorting: false,
                header: 'First',
                id: 'first',
                meta: {
                    class: 'w-36'
                }
            },
            {
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('last_occurrence'),
                cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() }),
                enableSorting: false,
                header: 'Last',
                id: 'last',
                meta: {
                    class: 'w-36'
                }
            }
        );
    }

    return columns;
}

export function getTableContext<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(
    params: GetEventsParams,
    configureOptions: (options: TableOptions<TSummaryModel>) => TableOptions<TSummaryModel> = (options) => options
) {
    let _parameters = $state(params);
    let _pageCount = $state(0);
    let _columns = $state(getColumns<TSummaryModel>(untrack(() => _parameters.mode)));
    let _data = $state([] as TSummaryModel[]);
    let _loading = $state(false);
    let _meta = $state({} as FetchClientResponse<unknown>['meta']);

    const [columnVisibility, setColumnVisibility] = createPersistedTableState('events-column-visibility', <VisibilityState>{});
    const [pagination, setPagination] = createTableState<PaginationState>({
        pageIndex: 0,
        pageSize: untrack(() => _parameters.limit) ?? DEFAULT_LIMIT
    });
    const [sorting, setSorting] = createTableState<ColumnSort[]>([
        {
            desc: true,
            id: 'date'
        }
    ]);
    const [rowSelection, setRowSelection] = createTableState<RowSelectionState>({});
    const onPaginationChange = (updaterOrValue: Updater<PaginationState>) => {
        if (_loading) {
            return;
        }

        _loading = true;
        const previousPageIndex = pagination().pageIndex;
        setPagination(updaterOrValue);

        const currentPageInfo = pagination();
        const nextLink = _meta.links?.next?.after as string;
        const previousLink = _meta.links?.previous?.before as string;

        _parameters = {
            ..._parameters,
            after: currentPageInfo.pageIndex > previousPageIndex ? nextLink : undefined,
            before: currentPageInfo.pageIndex < previousPageIndex && currentPageInfo.pageIndex > 0 ? previousLink : undefined,
            limit: currentPageInfo.pageSize,
            page: !nextLink && !previousLink && currentPageInfo.pageIndex !== 0 ? currentPageInfo.pageIndex + 1 : undefined
        };
    };

    const onSortingChange = (updaterOrValue: Updater<ColumnSort[]>) => {
        setSorting(updaterOrValue);

        _parameters = {
            ..._parameters,
            after: undefined,
            before: undefined,
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
