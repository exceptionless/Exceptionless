import type { FetchClientResponse } from '@exceptionless/fetchclient';

import NumberFormatter from '$comp/formatters/Number.svelte';
import TimeAgo from '$comp/formatters/TimeAgo.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { nameof } from '$lib/utils';
import { DEFAULT_LIMIT } from '$shared/api';
import { persisted } from '$shared/persisted.svelte';
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

import type { GetEventsMode, IGetEventsParams } from '../../models/index';
import type { EventSummaryModel, StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '../summary/index';

import Summary from '../summary/Summary.svelte';
import EventsUserIdentitySummaryCell from './EventsUserIdentitySummaryCell.svelte';
import StackStatusCell from './StackStatusCell.svelte';
import StackUsersSummaryCell from './StackUsersSummaryCell.svelte';

export function getColumns<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(mode: GetEventsMode = 'summary'): ColumnDef<TSummaryModel>[] {
    const columns: ColumnDef<TSummaryModel>[] = [
        {
            cell: (props) =>
                renderComponent(Checkbox, {
                    'aria-label': 'Select row',
                    checked: props.row.getIsSelected() ? true : props.row.getIsSomeSelected() ? 'indeterminate' : false,
                    class: 'translate-y-[2px]',
                    disabled: !props.row.getCanSelect(),
                    onCheckedChange: (checked: 'indeterminate' | boolean) => props.row.getToggleSelectedHandler()({ target: { checked } })
                }),
            enableHiding: false,
            enableSorting: false,
            header: ({ table }) =>
                renderComponent(Checkbox, {
                    checked: table.getIsAllRowsSelected() ? true : table.getIsSomeRowsSelected() ? 'indeterminate' : false,
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
                cell: (prop) => renderComponent(StackStatusCell, { value: prop.getValue<string>() }),
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
    params: IGetEventsParams,
    configureOptions: (options: TableOptions<TSummaryModel>) => TableOptions<TSummaryModel> = (options) => options
) {
    let parameters = $state(params);
    let pageCount = $state(0);
    let data = $state([] as TSummaryModel[]);
    let loading = $state(false);
    let meta = $state({} as FetchClientResponse<unknown>['meta']);

    const columns = getColumns<TSummaryModel>(parameters.mode);
    const [columnVisibility, setColumnVisibility] = createPersistedTableState('events-column-visibility', <VisibilityState>{});
    const [pagination, setPagination] = createTableState<PaginationState>({
        pageIndex: 0,
        pageSize: parameters.limit ?? DEFAULT_LIMIT
    });
    const [sorting, setSorting] = createTableState<ColumnSort[]>([
        {
            desc: true,
            id: 'date'
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
            after: currentPageInfo.pageIndex > previousPageIndex ? (meta.links.next?.after as string) : undefined,
            before: currentPageInfo.pageIndex < previousPageIndex && currentPageInfo.pageIndex > 0 ? (meta.links.previous?.before as string) : undefined,
            limit: currentPageInfo.pageSize
        };
    };

    const onSortingChange = (updaterOrValue: Updater<ColumnSort[]>) => {
        setSorting(updaterOrValue);

        parameters = {
            ...parameters,
            after: undefined,
            before: undefined,
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
            return pageCount;
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
            return data;
        },
        set data(value) {
            data = value;
        },
        get limit() {
            return parameters.limit ?? DEFAULT_LIMIT;
        },
        set limit(value) {
            parameters.limit = value;
            setPagination({ pageIndex: 0, pageSize: value });
        },
        get loading() {
            return loading;
        },
        get meta() {
            return meta;
        },
        set meta(value) {
            meta = value;

            const limit = parameters.limit ?? DEFAULT_LIMIT;
            const total = (meta?.total as number) ?? 0;
            pageCount = Math.ceil(total / limit);

            loading = false;
        },
        options,
        get pageCount() {
            return pageCount;
        },
        get parameters() {
            return parameters;
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
    const persistedValue = persisted<T>(key, initialValue);

    return [
        () => persistedValue.value,
        (updater: Updater<T>) => {
            if (updater instanceof Function) {
                persistedValue.value = updater(persistedValue.value);
            } else {
                persistedValue.value = updater;
            }
        }
    ];
}
