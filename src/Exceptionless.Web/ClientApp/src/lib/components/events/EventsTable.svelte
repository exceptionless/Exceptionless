<script lang="ts">
	import Time from 'svelte-time';
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
	import StackUsersSummary from './StackUsersSummaryColumn.svelte';
	import Pager from '$comp/table/Pager.svelte';
	import NumberFormatter from '$comp/formatters/NumberFormatter.svelte';
	import EventsUserIdentitySummaryColumn from './EventsUserIdentitySummaryColumn.svelte';
	import PagerSummary from '$comp/table/PagerSummary.svelte';
	import Loading from '$comp/Loading.svelte';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import Table from '$comp/table/Table.svelte';
	import { createEventDispatcher } from 'svelte';
	import {
		DEFAULT_LIMIT,
		hasNextPage,
		hasPreviousPage,
		canNavigateToFirstPage
	} from '$lib/helpers/api';
	import { type FetchClientResponse, FetchClient } from '$api/FetchClient';

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
				cell: (prop) =>
					flexRender(Time, { live: true, relative: true, timestamp: prop.getValue() })
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
				cell: (prop) => flexRender(StackUsersSummary, { summary: prop.row.original })
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
				cell: (prop) =>
					flexRender(Time, { live: true, relative: true, timestamp: prop.getValue() })
			},
			{
				id: 'last',
				header: 'Last',
				enableSorting: false,
				meta: {
					class: 'w-36'
				},
				accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('last_occurrence'),
				cell: (prop) =>
					flexRender(Time, { live: true, relative: true, timestamp: prop.getValue() })
			}
		);
	}

	let columnVisibility: VisibilityState = {};
	const setColumnVisibility = (updaterOrValue: Updater<VisibilityState>) => {
		if (updaterOrValue instanceof Function) {
			columnVisibility = updaterOrValue(columnVisibility);
		} else {
			columnVisibility = updaterOrValue;
		}

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				columnVisibility
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
			columnVisibility,
			sorting
		},
		onColumnVisibilityChange: setColumnVisibility,
		onSortingChange: setSorting,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow, _, __) => originalRow.id
	});

	const table = createSvelteTable<SummaryModel<SummaryTemplateKeys>>(options);
	let page = 0;

	const api = new FetchClient();
	const loading = api.loading;
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
		page = 0;
		parameters.update((params) => ({
			...params,
			before: undefined,
			after: undefined
		}));
	}

	function onPreviousPage() {
		page = Math.max(page - 1, 0);
		parameters.update((params) => ({
			...params,
			before: response?.links.next?.before,
			after: undefined
		}));
	}

	function onNextPage() {
		page = page + 1;
		parameters.update((params) => ({
			...params,
			before: undefined,
			after: response?.links.next?.after
		}));
	}

	const dispatch = createEventDispatcher();
</script>

<Table {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}>
	<div slot="header">
		<slot name="header" {table} problem={response?.problem} />
	</div>
	<div slot="footer">
		<slot name="footer" {table} problem={response?.problem} />
	</div>
</Table>

<div class="flex flex-1 items-center justify-between text-xs text-gray-700">
	<div class="py-2">
		{#if $loading}
			<Loading></Loading>
		{:else if response?.problem?.errors.general}
			<ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
		{/if}
	</div>

	{#if response?.total}
		<PagerSummary
			{page}
			pageTotal={response?.data?.length || 0}
			limit={DEFAULT_LIMIT}
			total={response?.total || 0}
		></PagerSummary>

		<div class="py-2">
			<Pager
				canNavigateToFirstPage={canNavigateToFirstPage(page)}
				on:navigatetofirstpage={() => onNavigateToFirstPage()}
				hasPrevious={hasPreviousPage(page)}
				on:previous={() => onPreviousPage()}
				hasNext={hasNextPage(
					page,
					response?.data?.length || 0,
					DEFAULT_LIMIT,
					response?.total || 0
				)}
				on:next={() => onNextPage()}
			></Pager>
		</div>
	{/if}
</div>
