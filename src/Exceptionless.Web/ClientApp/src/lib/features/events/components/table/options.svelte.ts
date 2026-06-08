import type { StackStatus } from '$features/stacks/models';

import NumberFormatter from '$comp/formatters/number.svelte';
import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { nameof } from '$lib/utils';
import { type ColumnDef, type ColumnVisibilityState, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

import type { GetEventsMode } from '../../api.svelte';
import type { EventSummaryModel, StackSummaryModel, SummaryModel, SummaryTemplateKeys } from '../summary/index';

import LogLevel from '../log-level.svelte';
import Summary from '../summary/summary.svelte';
import EventsUserIdentitySummaryCell from './events-user-identity-summary-cell.svelte';
import StackStatusCell from './stack-status-cell.svelte';
import StackUsersSummaryCell from './stack-users-summary-cell.svelte';

export const defaultEventColumnVisibility: ColumnVisibilityState = {
    exception_type: false,
    level: false,
    message: false,
    name: false,
    source: false,
    type: false
};

export function getColumns<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(
    mode: GetEventsMode = 'summary',
    options?: { showType?: boolean }
): ColumnDef<StockFeatures, TSummaryModel, unknown>[] {
    const showType = options?.showType ?? true;
    const columns: ColumnDef<StockFeatures, TSummaryModel, unknown>[] = [
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
            cell: (prop) => renderComponent(Summary, { showStatus: false, showType, summary: prop.row.original }),
            header: 'Summary',
            id: 'summary',
            meta: {
                class: 'w-full'
            }
        }
    ];

    const isEventSummary = mode === 'summary';
    if (isEventSummary) {
        columns.push(
            {
                cell: (prop) => renderComponent(EventsUserIdentitySummaryCell, { summary: prop.row.original }),
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
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Message'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                enableSorting: false,
                header: 'Message',
                id: 'message',
                meta: {
                    class: 'w-full'
                }
            },
            {
                accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('type'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                header: 'Type',
                id: 'type',
                meta: {
                    class: 'w-28'
                }
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Type'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                header: 'Exception Type',
                id: 'exception_type',
                meta: {
                    class: 'w-36'
                }
            },
            {
                accessorFn: (row) => getSource(row),
                cell: (prop) => formatTextColumn(prop.getValue()),
                header: 'Source',
                id: 'source',
                meta: {
                    class: 'w-40'
                }
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Name'),
                cell: (prop) => formatTextColumn(prop.getValue()),
                enableSorting: false,
                header: 'Name',
                id: 'name',
                meta: {
                    class: 'w-40'
                }
            },
            {
                accessorFn: (row) => getSummaryDataValue(row, 'Level'),
                cell: (prop) => renderComponent(LogLevel, { level: prop.getValue<string | undefined>() }),
                header: 'Level',
                id: 'level',
                meta: {
                    class: 'w-[4.5rem] min-w-[4.5rem] max-w-[4.5rem] px-1 text-center'
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

function formatTextColumn(value: unknown): string {
    return typeof value === 'string' && value.length > 0 ? value : '—';
}

function getSource<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(summary: TSummaryModel): string | undefined {
    return getSummaryDataValue(summary, 'SourceShortName') ?? getSummaryDataValue(summary, 'Source');
}

function getSummaryDataValue<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(summary: TSummaryModel, key: string): string | undefined {
    const value = (summary.data as Record<string, unknown>)[key];
    return typeof value === 'string' && value.length > 0 ? value : undefined;
}
