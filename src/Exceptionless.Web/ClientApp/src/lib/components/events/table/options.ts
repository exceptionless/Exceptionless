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
import EventsUserIdentitySummaryCell from './EventsUserIdentitySummaryCell.svelte';
import TimeAgo from '$comp/formatters/TimeAgo.svelte';
import StackUsersSummaryCell from './StackUsersSummaryCell.svelte';
import { DEFAULT_LIMIT } from '$lib/helpers/api';
import type { FetchClientResponse } from '$api/FetchClient';
import { Checkbox } from '$comp/ui/checkbox';
import StackStatusCell from './StackStatusCell.svelte';

export function getColumns<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(
	mode: GetEventsMode = 'summary'
): ColumnDef<TSummaryModel>[] {
	const columns: ColumnDef<TSummaryModel>[] = [
		{
			id: 'select',
			header: ({ table }) =>
				renderComponent(Checkbox, {
					checked: table.getIsAllRowsSelected()
						? true
						: table.getIsSomeRowsSelected()
							? 'indeterminate'
							: false,
					onCheckedChange: (checked: boolean | 'indeterminate') =>
						table.getToggleAllRowsSelectedHandler()({ target: { checked } })
				}),
			cell: (props) =>
				renderComponent(Checkbox, {
					checked: props.row.getIsSelected()
						? true
						: props.row.getIsSomeSelected()
							? 'indeterminate'
							: false,
					disabled: !props.row.getCanSelect(),
					onCheckedChange: (checked: boolean | 'indeterminate') =>
						props.row.getToggleSelectedHandler()({ target: { checked } }),
					'aria-label': 'Select row',
					class: 'translate-y-[2px]'
				}),
			enableHiding: false,
			enableSorting: false
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
				cell: (prop) =>
					renderComponent(EventsUserIdentitySummaryCell, { summary: prop.row.original })
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
				cell: (prop) =>
					renderComponent(StackUsersSummaryCell, { summary: prop.row.original })
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

export function getOptions<TSummaryModel extends SummaryModel<SummaryTemplateKeys>>(
	parameters: Writable<IGetEventsParams>,
	configureOptions: (options: TableOptions<TSummaryModel>) => TableOptions<TSummaryModel> = (
		options
	) => options
) {
	const columns = getColumns<TSummaryModel>(get(parameters).mode);
	const columnVisibility = persisted('events-column-visibility', <VisibilityState>{});

	const onColumnVisibilityChange = (updaterOrValue: Updater<VisibilityState>) => {
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

	let pagination: PaginationState = {
		pageIndex: 0,
		pageSize: get(parameters).limit ?? DEFAULT_LIMIT
	};

	const onPaginationChange = (updaterOrValue: Updater<PaginationState>) => {
		const { loading = false } = (get(options).meta as { loading: boolean }) ?? {
			loading: false
		};
		if (loading) {
			return;
		}

		const previousPageIndex = pagination.pageIndex;
		if (updaterOrValue instanceof Function) {
			pagination = updaterOrValue(pagination);
		} else {
			pagination = updaterOrValue;
		}

		// Force a reset of the row selection state until we get smarter about it.
		rowSelection = {};

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				pagination,
				rowSelection
			},
			meta: {
				...old.meta,
				loading: true
			}
		}));

		const meta = get(options).meta as FetchClientResponse<unknown>['meta'];
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

	let rowSelection: RowSelectionState = {};

	const onRowSelectionChange = (updaterOrValue: Updater<RowSelectionState>) => {
		if (updaterOrValue instanceof Function) {
			rowSelection = updaterOrValue(rowSelection);
		} else {
			rowSelection = updaterOrValue;
		}

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				rowSelection: rowSelection
			}
		}));
	};

	let sorting: ColumnSort[] = [
		{
			id: 'date',
			desc: true
		}
	];

	const onSortingChange = (updaterOrValue: Updater<ColumnSort[]>) => {
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

	const options = writable<TableOptions<TSummaryModel>>(
		configureOptions({
			columns,
			data: [],
			enableRowSelection: true,
			enableMultiRowSelection: true,
			enableSortingRemoval: false,
			manualSorting: true,
			manualPagination: true,
			state: {
				columnVisibility: get(columnVisibility),
				rowSelection,
				sorting
			},
			getCoreRowModel: getCoreRowModel(),
			getRowId: (originalRow) => originalRow.id,
			onColumnVisibilityChange,
			onPaginationChange,
			onRowSelectionChange,
			onSortingChange
		})
	);

	return options;
}
