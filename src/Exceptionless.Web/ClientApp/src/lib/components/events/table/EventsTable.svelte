<script lang="ts">
	import { writable, type Readable } from 'svelte/store';
	import {
		createSvelteTable,
		flexRender,
		getCoreRowModel,
		type ColumnDef,
		type TableOptions,
		type Updater,
		type VisibilityState,
		type ColumnSort
	} from '@tanstack/svelte-table';
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
	import { createEventDispatcher } from 'svelte';
	import { DEFAULT_LIMIT } from '$lib/helpers/api';
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import TableWithPaging from '$comp/table/TableWithPaging.svelte';
	import TableWithPagingFooter from '$comp/table/TableWithPagingFooter.svelte';
	import { persisted } from 'svelte-local-storage-store';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import StackUsersSummaryColumn from './StackUsersSummaryColumn.svelte';

	export let mode: GetEventsMode = 'summary';
	export let filter: Readable<string>;
	export let time: Readable<string>;

	const defaultColumns: ColumnDef<SummaryModel<SummaryTemplateKeys>>[] = [
		{
			header: 'Summary',
			enableHiding: false,
			cell: (prop) => flexRender(Summary, { summary: prop.row.original })
		}
	];

	const isEventSummary = mode === 'summary';
	if (isEventSummary) {
		defaultColumns.push(
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
		defaultColumns.push(
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

	const columnVisibility = persisted('events-column-visibility', <VisibilityState>{});
	const setColumnVisibility = (updaterOrValue: Updater<VisibilityState>) => {
		if (updaterOrValue instanceof Function) {
			columnVisibility.update(() => updaterOrValue($columnVisibility));
		} else {
			columnVisibility.update(() => updaterOrValue);
		}

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				...{ columnVisibility: $columnVisibility }
			}
		}));
	};

	let sorting: ColumnSort[] = [
		{
			id: 'date',
			desc: true
		}
	];
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
		columns: defaultColumns,
		data: [],
		enableSortingRemoval: false,
		manualSorting: true,
		state: {
			columnVisibility: $columnVisibility,
			sorting
		},
		onColumnVisibilityChange: setColumnVisibility,
		onSortingChange: setSorting,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow) => originalRow.id
	});

	const table = createSvelteTable<SummaryModel<SummaryTemplateKeys>>(options);
	const page = writable(0);

	let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;
	const parameters = writable<IGetEventsParams>({ mode });
	parameters.subscribe(async () => await loadData());
	filter.subscribe(async () => await loadData());
	time.subscribe(async () => await loadData());

	async function loadData() {
		if ($loading) {
			return;
		}

		response = await api.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
			params: {
				mode: 'summary',
				filter: $filter,
				time: $time,
				...$parameters
			}
		});

		if (response.ok) {
			options.update((options) => ({
				...options,
				data: response.data || []
			}));
		}
	}

	function onNavigateToFirstPage() {
		page.update(() => 0);
		parameters.update((params) => ({
			...params,
			before: undefined,
			after: undefined
		}));
	}

	function onPreviousPage() {
		page.update((page) => Math.max(page - 1, 0));
		parameters.update((params) => ({
			...params,
			before: response?.links.next?.before,
			after: undefined
		}));
	}

	function onNextPage() {
		page.update((page) => page + 1);
		parameters.update((params) => ({
			...params,
			before: undefined,
			after: response?.links.next?.after
		}));
	}

	const dispatch = createEventDispatcher();
</script>

<TableWithPaging
	{table}
	loading={$loading}
	error={response?.problem?.errors.general}
	page={$page}
	pageTotal={response?.data?.length || 0}
	limit={DEFAULT_LIMIT}
	total={response?.total || 0}
	{onNavigateToFirstPage}
	{onPreviousPage}
	{onNextPage}
	on:rowclick={(event) => dispatch('rowclick', event.detail)}
>
	<slot slot="header" name="header" {table} />
	<slot
		slot="footer"
		name="footer"
		{table}
		loading={$loading}
		error={response?.problem?.errors.general}
		page={$page}
		pageTotal={response?.data?.length || 0}
		limit={DEFAULT_LIMIT}
		total={response?.total || 0}
		{onNavigateToFirstPage}
		{onPreviousPage}
		{onNextPage}
	>
		<TableWithPagingFooter
			loading={$loading}
			error={response?.problem?.errors.general}
			page={$page}
			pageTotal={response?.data?.length || 0}
			limit={DEFAULT_LIMIT}
			total={response?.total || 0}
			{onNavigateToFirstPage}
			{onPreviousPage}
			{onNextPage}
		></TableWithPagingFooter>
	</slot>
</TableWithPaging>
