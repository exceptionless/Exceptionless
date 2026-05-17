import type { Stack } from '$features/stacks/models';

import NumberFormatter from '$comp/formatters/number.svelte';
import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { nameof } from '$lib/utils';
import { type ColumnDef, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

import StackSeverityCell from './stack-severity-cell.svelte';
import StackStatusCell from './stack-status-cell.svelte';
import StackTagsCell from './stack-tags-cell.svelte';
import StackTypeBadge from './stack-type-badge.svelte';

export function getColumns(onTagClick?: (tag: string) => void): ColumnDef<StockFeatures, Stack, unknown>[] {
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
            accessorKey: nameof<Stack>('title'),
            enableHiding: false,
            header: 'Title',
            id: 'title'
        },
        {
            accessorKey: nameof<Stack>('status'),
            cell: (prop) => renderComponent(StackStatusCell, { value: prop.getValue<string>() }),
            header: 'Status',
            id: 'status',
            meta: {
                class: 'w-28'
            }
        },
        {
            accessorKey: nameof<Stack>('type'),
            cell: (prop) => renderComponent(StackTypeBadge, { value: prop.getValue<string>() }),
            header: 'Type',
            id: 'type',
            meta: {
                class: 'w-24'
            }
        },
        {
            accessorKey: nameof<Stack>('occurrences_are_critical'),
            cell: (prop) => renderComponent(StackSeverityCell, { isCritical: prop.getValue<boolean>() }),
            header: 'Critical',
            id: 'critical',
            meta: {
                class: 'w-24'
            }
        },
        {
            accessorKey: nameof<Stack>('tags'),
            cell: (prop) => renderComponent(StackTagsCell, { onTagClick, tags: prop.getValue<string[]>() }),
            header: 'Tags',
            id: 'tags',
            meta: {
                class: 'w-40'
            }
        },
        {
            accessorKey: nameof<Stack>('total_occurrences'),
            cell: (prop) => renderComponent(NumberFormatter, { value: prop.getValue<number>() }),
            header: 'Events',
            id: 'events',
            meta: {
                class: 'w-24 text-right'
            }
        },
        {
            accessorKey: nameof<Stack>('first_occurrence'),
            cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() }),
            header: 'First',
            id: 'first',
            meta: {
                class: 'w-36'
            }
        },
        {
            accessorKey: nameof<Stack>('last_occurrence'),
            cell: (prop) => renderComponent(TimeAgo, { value: prop.getValue<string>() }),
            header: 'Last',
            id: 'last',
            meta: {
                class: 'w-36'
            }
        },
        {
            accessorKey: nameof<Stack>('fixed_in_version'),
            header: 'Fixed In',
            id: 'fixed_in_version',
            meta: {
                class: 'w-28'
            }
        }
    ];
}
