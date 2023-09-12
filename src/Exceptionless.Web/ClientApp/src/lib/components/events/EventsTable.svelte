<script lang="ts">
	import { createEventDispatcher } from 'svelte';
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
	import Pager from '$comp/Pager.svelte';
	import NumberFormatter from '$comp/formatters/NumberFormatter.svelte';
	import EventsUserIdentitySummaryColumn from './EventsUserIdentitySummaryColumn.svelte';
	import PagerSummary from '$comp/PagerSummary.svelte';

	export let mode: GetEventsMode = 'summary';
	let eventParams: IGetEventsParams = { mode, before: undefined, after: undefined };
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
	const dispatch = createEventDispatcher();

	let page = 0;
</script>

<table class="table table-zebra table-xs border">
	<thead>
		{#each $table.getHeaderGroups() as headerGroup}
			<tr>
				{#each headerGroup.headers as header}
					<th>
						{#if !header.isPlaceholder}
							<svelte:component
								this={flexRender(
									header.column.columnDef.header,
									header.getContext()
								)}
							/>
						{/if}
					</th>
				{/each}
			</tr>
		{/each}
	</thead>
	<tbody>
		{#each $table.getRowModel().rows as row}
			<tr
				class="hover cursor-pointer"
				on:click|preventDefault={() => dispatch('rowclick', row.original)}
			>
				{#each row.getVisibleCells() as cell}
					<td>
						<svelte:component
							this={flexRender(cell.column.columnDef.cell, cell.getContext())}
						/>
					</td>
				{/each}
			</tr>
		{/each}
	</tbody>
</table>
{#if $queryResult?.data?.total}
	<div class="hidden sm:flex sm:flex-1 sm:items-center sm:justify-between">
		<PagerSummary
			{page}
			pageSize={10}
			pageTotal={$queryResult.data.data?.length || 0}
			total={$queryResult.data.total}
		></PagerSummary>
		<div class="py-2">
			<Pager
				hasPrevious={page > 0}
				on:previous={() => {
					page = Math.max(page - 1, 0);
					eventParams = {
						...eventParams,
						before: $queryResult.data?.links.previous?.before,
						after: undefined
					};
				}}
				hasNext={!!$queryResult.data.links.next}
				on:next={() => {
					page = page + 1;
					eventParams = {
						...eventParams,
						before: undefined,
						after: $queryResult.data?.links.next?.after
					};
				}}
			></Pager>
		</div>
	</div>
{/if}

<!-- TODO: Error and loading indicators -->
<!-- TODO: move this into the table -->
