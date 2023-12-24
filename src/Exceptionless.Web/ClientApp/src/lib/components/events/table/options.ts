import {
	flexRender,
	type ColumnDef,
	getCoreRowModel,
	type ColumnSort,
	type PaginationState,
	type TableOptions,
	type Updater,
	type VisibilityState
} from '@tanstack/svelte-table';
import { persisted } from 'svelte-local-storage-store';
import { get, writable, type Writable } from 'svelte/store';

import type {
	EventSummaryModel,
	GetEventsMode,
	IGetEventsParams,
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
import { DEFAULT_LIMIT } from '$lib/helpers/api';
import type { FetchClientResponse } from '$api/FetchClient';

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

export function getOptions(parameters: Writable<IGetEventsParams>) {
	const columns = getColumns(get(parameters).mode);
	const columnVisibility = persisted('events-column-visibility', <VisibilityState>{});

	const setColumnVisibility = (updaterOrValue: Updater<VisibilityState>) => {
		if (updaterOrValue instanceof Function) {
			columnVisibility.update(() => updaterOrValue(get(columnVisibility)));
		} else {
			columnVisibility.update(() => updaterOrValue);
		}

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				...{ columnVisibility: get(columnVisibility) }
			}
		}));
	};

	let sorting: ColumnSort[] = [
		{
			id: 'date',
			desc: true
		}
	];

	let pagination: PaginationState = {
		pageIndex: 0,
		pageSize: DEFAULT_LIMIT
	};
	const setPagination = (updaterOrValue: Updater<PaginationState>) => {
		const previousPageIndex = pagination.pageIndex;
		if (updaterOrValue instanceof Function) {
			pagination = updaterOrValue(pagination);
		} else {
			pagination = updaterOrValue;
		}

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				pagination: pagination
			}
		}));

		const meta = get(options).meta as FetchClientResponse<unknown>['meta'];
		console.log({ updatedPageIndex: pagination.pageIndex, previousPageIndex, meta });
		parameters.update((params) => ({
			...params,
			before:
				pagination.pageIndex < previousPageIndex && pagination.pageIndex > 0
					? (meta?.links.previous?.before as string)
					: undefined,
			after:
				pagination.pageIndex > previousPageIndex
					? (meta?.links.next?.after as string)
					: undefined,
			limit: pagination.pageSize
		}));
	};

	const setSorting = (updaterOrValue: Updater<ColumnSort[]>) => {
		if (updaterOrValue instanceof Function) {
			sorting = updaterOrValue(sorting);
		} else {
			sorting = updaterOrValue;
		}

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				sorting
			}
		}));

		parameters.update((params) => ({
			...params,
			before: undefined,
			after: undefined,
			sort:
				sorting.length > 0
					? sorting.map((sort) => `${sort.desc ? '-' : ''}${sort.id}`).join(',')
					: undefined
		}));
	};

	const options = writable<TableOptions<SummaryModel<SummaryTemplateKeys>>>({
		columns,
		data: [],
		enableSortingRemoval: false,
		manualSorting: true,
		manualPagination: true,
		state: {
			columnVisibility: get(columnVisibility),
			sorting
		},
		onColumnVisibilityChange: setColumnVisibility,
		onPaginationChange: setPagination,
		onSortingChange: setSorting,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow) => originalRow.id
	});

	return options;
}
