import {
    type ColumnDef,
    getCoreRowModel,
    type ColumnSort,
    type PaginationState,
    type TableOptions,
    type Updater,
    type VisibilityState,
    renderComponent,
    type RowSelectionState
} from '$comp/tanstack-table-svelte5';
import { persisted } from 'svelte-persisted-store';
import { get } from 'svelte/store';

import type { EventSummaryModel, GetEventsMode, IGetEventsParams, StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
import Summary from '$comp/events/summary/Summary.svelte';
import { nameof } from '$lib/utils';
import NumberFormatter from '$comp/formatters/Number.svelte';
import EventsUserIdentitySummaryCell from './EventsUserIdentitySummaryCell.svelte';
import TimeAgo from '$comp/formatters/TimeAgo.svelte';
import StackUsersSummaryCell from './StackUsersSummaryCell.svelte';
import { DEFAULT_LIMIT } from '$lib/helpers/api';
import type { FetchClientResponse } from '@exceptionless/fetchclient';
import { Checkbox } from '$comp/ui/checkbox';
import StackStatusCell from './StackStatusCell.svelte';

export function getColumns<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(mode: GetEventsMode = 'summary'): ColumnDef<TSummaryModel>[] {
    const columns: ColumnDef<TSummaryModel>[] = [
        {
            id: 'select',
            header: ({ table }) =>
                renderComponent(Checkbox, {
                    checked: table.getIsAllRowsSelected() ? true : table.getIsSomeRowsSelected() ? 'indeterminate' : false,
                    onCheckedChange: (checked: boolean | 'indeterminate') => table.getToggleAllRowsSelectedHandler()({ target: { checked } })
                }),
            cell: (props) =>
                renderComponent(Checkbox, {
                    checked: props.row.getIsSelected() ? true : props.row.getIsSomeSelected() ? 'indeterminate' : false,
                    disabled: !props.row.getCanSelect(),
                    onCheckedChange: (checked: boolean | 'indeterminate') => props.row.getToggleSelectedHandler()({ target: { checked } }),
                    'aria-label': 'Select row',
                    class: 'translate-y-[2px]'
                }),
            enableHiding: false,
            enableSorting: false,
            meta: {
                class: 'w-6'
            }
        },
        {
            header: 'Summary',
            enableHiding: false,
            cell: (prop) => renderComponent(Summary, { summary: prop.row.original })
        }
    ];

    const isEventSummary = mode === 'summary';
    if (isEventSummary) {
        columns.push(
            {
                id: 'user',
                header: 'User',
                enableSorting: false,
                meta: {
                    class: 'w-28'
                },
                cell: (prop) => renderComponent(EventsUserIdentitySummaryCell, { summary: prop.row.original })
            },
            {
                id: 'date',
                header: 'Date',
                accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('date'),
                meta: {
                    class: 'w-36'
                },
                cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() })
            }
        );
    } else {
        columns.push(
            {
                id: 'status',
                header: 'Status',
                enableSorting: false,
                meta: {
                    class: 'w-36'
                },
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('status'),
                cell: (prop) => renderComponent(StackStatusCell, { value: prop.getValue<string>() })
            },
            {
                id: 'users',
                header: 'Users',
                enableSorting: false,
                meta: {
                    class: 'w-24'
                },
                cell: (prop) => renderComponent(StackUsersSummaryCell, { summary: prop.row.original })
            },
            {
                id: 'events',
                header: 'Events',
                enableSorting: false,
                meta: {
                    class: 'w-24'
                },
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('total'),
                cell: (prop) => renderComponent(NumberFormatter, { value: prop.getValue<number>() })
            },
            {
                id: 'first',
                header: 'First',
                enableSorting: false,
                meta: {
                    class: 'w-36'
                },
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('first_occurrence'),
                cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() })
            },
            {
                id: 'last',
                header: 'Last',
                enableSorting: false,
                meta: {
                    class: 'w-36'
                },
                accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('last_occurrence'),
                cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() })
            }
        );
    }

    return columns;
}

export function getTableContext<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(
    parameters: IGetEventsParams,
    configureOptions: (options: TableOptions<TSummaryModel>) => TableOptions<TSummaryModel> = (options) => options
) {
    let pageCount = $state(0);
    let data = $state([] as TSummaryModel[]);
    let loading = $state(false);
    let meta = $state.frozen({} as FetchClientResponse<unknown>['meta']);

    const columns = getColumns<TSummaryModel>(parameters.mode);
    const [columnVisibility, setColumnVisibility] = createPersistedTableState('events-column-visibility', <VisibilityState>{});
    const [pagination, setPagination] = createTableState<PaginationState>({
        pageIndex: 0,
        pageSize: parameters.limit ?? DEFAULT_LIMIT
    });
    const [sorting, setSorting] = createTableState<ColumnSort[]>([
        {
            id: 'date',
            desc: true
        }
    ]);
    const [rowSelection, setRowSelection] = createTableState<RowSelectionState>({});
    const onPaginationChange = (updaterOrValue: Updater<PaginationState>) => {
        if (loading) {
            return;
        }

        loading = true;
        const previousPageIndex = pagination().pageIndex;
        setPagination(updaterOrValue);

        // Force a reset of the row selection state until we get smarter about it.
        setRowSelection({});

        const currentPageInfo = pagination();
        parameters = {
            ...parameters,
            before: currentPageInfo.pageIndex < previousPageIndex && currentPageInfo.pageIndex > 0 ? (meta.links.previous?.before as string) : undefined,
            after: currentPageInfo.pageIndex > previousPageIndex ? (meta.links.next?.after as string) : undefined,
            limit: currentPageInfo.pageSize
        };
    };

    const onSortingChange = (updaterOrValue: Updater<ColumnSort[]>) => {
        setSorting(updaterOrValue);

        parameters = {
            ...parameters,
            before: undefined,
            after: undefined,
            sort:
                sorting().length > 0
                    ? sorting()
                          .map((sort) => `${sort.desc ? '-' : ''}${sort.id}`)
                          .join(',')
                    : undefined
        };
    };

    const options = configureOptions({
        columns,
        get data() {
            return data;
        },
        enableRowSelection: true,
        enableMultiRowSelection: true,
        enableSortingRemoval: false,
        manualSorting: true,
        manualPagination: true,
        get pageCount() {
            return pageCount;
        },
        state: {
            get columnVisibility() {
                return columnVisibility();
            },
            get rowSelection() {
                return rowSelection();
            },
            get sorting() {
                return sorting();
            }
        },
        getCoreRowModel: getCoreRowModel(),
        getRowId: (originalRow) => originalRow.id,
        onColumnVisibilityChange: setColumnVisibility,
        onPaginationChange,
        onRowSelectionChange: setRowSelection,
        onSortingChange
    });

    return {
        get data() {
            return data;
        },
        set data(value) {
            data = value;
        },
        get loading() {
            return loading;
        },
        set loading(value) {
            loading = value;
        },
        get meta() {
            return meta;
        },
        set meta(value) {
            meta = value;
        },
        options,
        get pageCount() {
            return pageCount;
        },
        set pageCount(value) {
            pageCount = value;
        }
    };
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

function createPersistedTableState<T>(key: string, initialValue: T): [() => T, (updater: Updater<T>) => void] {
    const value = persisted<T>(key, initialValue);

    return [
        () => get(value),
        (updater: Updater<T>) => {
            if (updater instanceof Function) {
                value.update(updater);
            } else {
                value.update(() => updater);
            }
        }
    ];
}
