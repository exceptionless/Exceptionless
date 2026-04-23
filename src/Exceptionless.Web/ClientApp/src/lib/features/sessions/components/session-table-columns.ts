import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { type ColumnDef, renderComponent } from '@tanstack/svelte-table';

import Summary from '$features/events/components/summary/summary.svelte';
import EventsUserIdentitySummaryCell from '$features/events/components/table/events-user-identity-summary-cell.svelte';

import SessionDurationCell from './session-duration-cell.svelte';

export function getSessionColumns(): ColumnDef<EventSummaryModel<SummaryTemplateKeys>>[] {
    return [
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
        },
        {
            cell: (prop) => renderComponent(SessionDurationCell, { summary: prop.row.original }),
            enableSorting: false,
            header: 'Duration',
            id: 'duration',
            meta: {
                class: 'w-36'
            }
        },
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
            accessorFn: (row) => row.date,
            cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() }),
            header: 'Date',
            id: 'date',
            meta: {
                class: 'w-36'
            }
        }
    ];
}
