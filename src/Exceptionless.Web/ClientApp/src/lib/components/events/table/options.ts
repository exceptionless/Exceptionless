import { flexRender, type ColumnDef } from '@tanstack/svelte-table';

import type {
	EventSummaryModel,
	GetEventsMode,
	StackSummaryModel,
	SummaryModel,
	SummaryTemplateKeys
} from '$lib/models/api';
import Summary from '$comp/events/summary/Summary.svelte';
import { nameof } from '$lib/utils';
import NumberFormatter from '$comp/formatters/Number.svelte';
import EventsUserIdentitySummaryColumn from './EventsUserIdentitySummaryColumn.svelte';
import TimeAgo from '$comp/formatters/TimeAgo.svelte';
import StackUsersSummaryColumn from './StackUsersSummaryColumn.svelte';

export function getColumns<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(
	mode: GetEventsMode = 'summary'
): ColumnDef<TSummaryModel>[] {
	const columns: ColumnDef<TSummaryModel>[] = [
		{
			header: 'Summary',
			enableHiding: false,
			cell: (prop) => flexRender(Summary, { summary: prop.row.original })
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
				cell: (prop) =>
					flexRender(EventsUserIdentitySummaryColumn, { summary: prop.row.original })
			},
			{
				id: 'date',
				header: 'Date',
				accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('date'),
				meta: {
					class: 'w-36'
				},
				cell: (prop) => flexRender(TimeAgo, { value: prop.getValue() })
			}
		);
	} else {
		columns.push(
			{
				id: 'users',
				header: 'Users',
				enableSorting: false,
				meta: {
					class: 'w-24'
				},
				cell: (prop) => flexRender(StackUsersSummaryColumn, { summary: prop.row.original })
			},
			{
				id: 'events',
				header: 'Events',
				enableSorting: false,
				meta: {
					class: 'w-24'
				},
				accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('total'),
				cell: (prop) => flexRender(NumberFormatter, { value: prop.getValue() as number })
			},
			{
				id: 'first',
				header: 'First',
				enableSorting: false,
				meta: {
					class: 'w-36'
				},
				accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('first_occurrence'),
				cell: (prop) => flexRender(TimeAgo, { value: prop.getValue() })
			},
			{
				id: 'last',
				header: 'Last',
				enableSorting: false,
				meta: {
					class: 'w-36'
				},
				accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('last_occurrence'),
				cell: (prop) => flexRender(TimeAgo, { value: prop.getValue() })
			}
		);
	}

	return columns;
}
