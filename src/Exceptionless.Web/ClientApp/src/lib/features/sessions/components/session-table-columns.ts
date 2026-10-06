import type { EventSummaryModel, SummaryTemplateKeys } from '$features/events/components/summary/index';

import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import Summary from '$features/events/components/summary/summary.svelte';
import EventsUserIdentitySummaryCell from '$features/events/components/table/events-user-identity-summary-cell.svelte';
import { type ColumnDef, type ColumnVisibilityState, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

import SessionDurationCell from './session-duration-cell.svelte';

export const defaultSessionColumnVisibility: ColumnVisibilityState = {};

export function getSessionColumns(): ColumnDef<StockFeatures, EventSummaryModel<SummaryTemplateKeys>, unknown>[] {
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
            enableResizing: false,
            enableSorting: false,
            header: ({ table }) =>
                renderComponent(Checkbox, {
                    checked: table.getIsAllRowsSelected(),
                    indeterminate: table.getIsSomeRowsSelected() && !table.getIsAllRowsSelected(),
                    onCheckedChange: (checked: 'indeterminate' | boolean) => table.getToggleAllRowsSelectedHandler()({ target: { checked } })
                }),
            id: 'select',
            meta: {
                class: 'w-6'
            }
        },
        {
            cell: (prop) => renderComponent(Summary, { showStatus: false, showType: false, summary: prop.row.original }),
            enableSorting: false,
            header: 'Summary',
            id: 'summary',
            maxSize: 1200,
            meta: {
                class: 'w-full',
                enableWrapping: true
            },
            minSize: 240,
            size: 480
        },
        {
            cell: (prop) => renderComponent(SessionDurationCell, { summary: prop.row.original }),
            enableSorting: false,
            header: 'Duration',
            id: 'duration',
            maxSize: 320,
            meta: {
                class: 'w-40'
            },
            minSize: 96,
            size: 160
        },
        {
            cell: (prop) => renderComponent(EventsUserIdentitySummaryCell, { summary: prop.row.original }),
            enableSorting: false,
            header: 'User',
            id: 'user',
            maxSize: 480,
            meta: {
                class: 'w-56',
                enableWrapping: true
            },
            minSize: 112,
            size: 224
        },
        {
            accessorFn: (row) => row.date,
            cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() }),
            header: 'Date',
            id: 'date',
            maxSize: 240,
            meta: {
                class: 'w-36'
            },
            minSize: 100,
            size: 144
        }
    ];
}
