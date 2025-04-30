import type { StackStatus } from '$features/stacks/models';

import NumberFormatter from '$comp/formatters/number.svelte';
import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { nameof } from '$lib/utils';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import type { GetEventsMode } from '../../api.svelte';
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
            cell: (prop) => renderComponent(Summary, { showStatus: false, summary: prop.row.original }),
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
