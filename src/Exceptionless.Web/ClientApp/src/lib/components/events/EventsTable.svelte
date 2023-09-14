<script lang="ts">
	import Time from 'svelte-time';
	import { writable } from 'svelte/store';
	import {
		createSvelteTable,
		flexRender,
		getCoreRowModel,
		type ColumnDef,
		type TableOptions,
		type Updater,
		type VisibilityState
	} from '@tanstack/svelte-table';
	import type {
		EventSummaryModel,
		StackSummaryModel,
		SummaryModel,
		SummaryTemplateKeys
	} from '$lib/models/api';
	import {
		useGetEventSummariesQuery,
		type IGetEventsParams,
		type GetEventsMode
	} from '$api/EventQueries';
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
	import { DEFAULT_LIMIT, hasNextPage, hasPreviousPage } from '$comp/table/pagination';

	export let mode: GetEventsMode = 'summary';
	let eventParams: IGetEventsParams = { mode };
	$: queryResult = useGetEventSummariesQuery(eventParams);

	const defaultColumns: ColumnDef<SummaryModel<SummaryTemplateKeys>>[] = [
		{
			header: 'Summary',
			cell: (prop) => flexRender(Summary, { summary: prop.row.original })
		}
	];

	const isEventSummary = mode === 'summary';
	if (isEventSummary) {
		defaultColumns.push(
			{
				id: 'user',
				header: 'User',
				cell: (prop) =>
					flexRender(EventsUserIdentitySummaryColumn, { summary: prop.row.original })
			},
			{
				id: 'date',
				header: 'Date',
				accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('date'),
				cell: (prop) =>
					flexRender(Time, { live: true, relative: true, timestamp: prop.getValue() })
			}
		);
	} else {
		defaultColumns.push(
			{
				id: 'users',
				header: 'Users',
				cell: (prop) => flexRender(StackUsersSummary, { summary: prop.row.original })
			},
			{
				id: 'events',
				header: 'Events',
				accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('total'),
				cell: (prop) => flexRender(NumberFormatter, { value: prop.getValue() as number })
			},
			{
				id: 'first',
				header: 'First',
				accessorKey: nameof<StackSummaryModel<SummaryTemplateKeys>>('first_occurrence'),
				cell: (prop) =>
					flexRender(Time, { live: true, relative: true, timestamp: prop.getValue() })
			},
			{
				id: 'last',
				header: 'Last',
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

	const options = writable<TableOptions<SummaryModel<SummaryTemplateKeys>>>({
		data: [],
		columns: defaultColumns,
		state: {
			columnVisibility
		},
		onColumnVisibilityChange: setColumnVisibility,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow, _, __) => originalRow.id
	});

	$: queryResult?.subscribe((result) => {
		options.update((options) => ({
			...options,
			data: result.data?.data ?? []
		}));
	});

	const table = createSvelteTable<SummaryModel<SummaryTemplateKeys>>(options);
	let page = 0;

	function onPreviousPage() {
		page = Math.max(page - 1, 0);
		eventParams = {
			...eventParams,
			before: $queryResult.data?.links.previous?.before,
			after: undefined
		};
	}

	function onNextPage() {
		page = page + 1;
		eventParams = {
			...eventParams,
			before: undefined,
			after: $queryResult.data?.links.next?.after
		};
	}

	const dispatch = createEventDispatcher();
</script>

<Table {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}></Table>

<div class="flex flex-1 items-center justify-between text-xs text-gray-700">
	<div class="py-2">
		{#if $queryResult.isLoading}
			<Loading></Loading>
		{:else if $queryResult.isError}
			<ErrorMessage message={$queryResult.error?.errors.general}></ErrorMessage>
		{/if}
	</div>
	{#if $queryResult.data?.total}
		<PagerSummary
			{page}
			pageTotal={$queryResult.data.data?.length || 0}
			limit={DEFAULT_LIMIT}
			total={$queryResult.data?.total || 0}
		></PagerSummary>

		<div class="py-2">
			<Pager
				hasPrevious={hasPreviousPage(page)}
				on:previous={() => onPreviousPage()}
				hasNext={hasNextPage(
					page,
					$queryResult.data.data?.length || 0,
					DEFAULT_LIMIT,
					$queryResult.data?.total || 0
				)}
				on:next={() => onNextPage()}
			></Pager>
		</div>
	{/if}
</div>
