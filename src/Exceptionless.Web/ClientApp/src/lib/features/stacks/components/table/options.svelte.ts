import type { GetProjectStacksParams } from '$features/stacks/api.svelte';
import type { Stack } from '$features/stacks/models';
import type { FetchClientResponse, ProblemDetails } from '@exceptionless/fetchclient';
import type { CreateQueryResult } from '@tanstack/svelte-query';

import NumberFormatter from '$comp/formatters/number.svelte';
import TimeAgo from '$comp/formatters/time-ago.svelte';
import { Checkbox } from '$comp/ui/checkbox';
import { getSharedTableOptions } from '$features/shared/table.svelte';
import { nameof } from '$lib/utils';
import { type ColumnDef, type ColumnVisibilityState, renderComponent, type StockFeatures } from '@tanstack/svelte-table';

import StackCriticalCell from './stack-critical-cell.svelte';
import StackStatusCell from './stack-status-cell.svelte';
import StackTagsCell from './stack-tags-cell.svelte';
import StackTypeBadge from './stack-type-badge.svelte';

export const defaultColumnVisibility: ColumnVisibilityState = {
    critical: false,
    events: true,
    first: false,
    fixed_in_version: false,
    last: true,
    select: true,
    status: true,
    tags: false,
    title: true,
    type: true
};

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
            cell: (prop) => renderComponent(StackStatusCell, { value: prop.getValue<Stack['status']>() }),
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
            cell: (prop) => renderComponent(StackCriticalCell, { isCritical: prop.getValue<boolean>() }),
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

export function getTableOptions(
    queryParameters: GetProjectStacksParams,
    queryResponse: CreateQueryResult<FetchClientResponse<Stack[]>, ProblemDetails>,
    onTagClick?: (tag: string) => void
) {
    return getSharedTableOptions<Stack>({
        columnPersistenceKey: 'project-issues-v2-column-visibility',
        get columns() {
            return getColumns(onTagClick);
        },
        defaultColumnVisibility,
        paginationStrategy: 'offset',
        get queryData() {
            return queryResponse.data?.data ?? [];
        },
        get queryMeta() {
            return queryResponse.data?.meta;
        },
        get queryParameters() {
            return queryParameters;
        }
    });
}
