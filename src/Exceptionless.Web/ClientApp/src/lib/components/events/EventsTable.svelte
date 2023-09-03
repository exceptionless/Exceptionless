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
	import StackUsersSummary from './summary/StackUsersSummary.svelte';

	export let mode: GetEventsMode = 'summary';
	const eventParams: IGetEventsParams = { mode };
	const queryResult = useGetEventSummariesQuery(eventParams);

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
				accessorFn: (row) => getUserColumnData(row)
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
				cell: (prop) => Intl.NumberFormat().format(prop.getValue() as number)
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

	queryResult.subscribe((result) => {
		options.update((options) => ({
			...options,
			data: result.data?.data ?? []
		}));
	});

	const table = createSvelteTable<SummaryModel<SummaryTemplateKeys>>(options);

	function getUserColumnData(row: SummaryModel<SummaryTemplateKeys>): string | undefined {
		console.log(row);
		if (!row?.data) {
			return;
		}

		if ('Identity' in row.data && row.data.Identity) {
			return row.data.Identity as string;
		}

		if ('Name' in row.data) {
			return row.data.Name as string;
		}
	}
</script>

<table class="table">
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
			<tr class="hover">
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
	<tfoot>
		{#each $table.getFooterGroups() as footerGroup}
			<tr>
				{#each footerGroup.headers as header}
					<th>
						{#if !header.isPlaceholder}
							<svelte:component
								this={flexRender(
									header.column.columnDef.footer,
									header.getContext()
								)}
							/>
						{/if}
					</th>
				{/each}
			</tr>
		{/each}
	</tfoot>
</table>
