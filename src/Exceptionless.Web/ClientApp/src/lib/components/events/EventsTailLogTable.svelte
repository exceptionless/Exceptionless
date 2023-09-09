<script lang="ts">
	import { createEventDispatcher, onMount } from 'svelte';
	import { writable } from 'svelte/store';
	import Time from 'svelte-time';
	import {
		createSvelteTable,
		flexRender,
		getCoreRowModel,
		type ColumnDef,
		type TableOptions,
		type Updater,
		type VisibilityState
	} from '@tanstack/svelte-table';
	import {
		ChangeType,
		type EventSummaryModel,
		type SummaryTemplateKeys,
		type WebSocketMessageValue
	} from '$lib/models/api';
	import type { IGetEventsParams } from '$api/EventQueries';
	import Summary from '$comp/events/summary/Summary.svelte';
	import { nameof } from '$lib/utils';
	import { FetchClient, ProblemDetails } from '$api/FetchClient';

	const defaultColumns: ColumnDef<EventSummaryModel<SummaryTemplateKeys>>[] = [
		{
			header: 'Summary',
			cell: (prop) => flexRender(Summary, { summary: prop.row.original })
		},
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
	];

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

	const options = writable<TableOptions<EventSummaryModel<SummaryTemplateKeys>>>({
		data: [],
		columns: defaultColumns,
		state: {
			columnVisibility
		},
		onColumnVisibilityChange: setColumnVisibility,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow, _, __) => originalRow.id
	});

	const table = createSvelteTable<EventSummaryModel<SummaryTemplateKeys>>(options);
	function getUserColumnData(row: EventSummaryModel<SummaryTemplateKeys>): string | undefined {
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

	const api = new FetchClient();
	const loading = api.loading;
	let problem = new ProblemDetails();
	const data = writable<EventSummaryModel<SummaryTemplateKeys>[]>([]);

	const defaultParams: IGetEventsParams = { mode: 'summary' };
	let lastUpdated: Date;
	let before: string | undefined;

	async function loadData() {
		if ($loading) {
			return;
		}

		const params = { ...defaultParams };
		if (before) {
			params.before = before;
		}

		const response = await api.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
			params
		});

		if (response.ok) {
			if (response.links.previous) {
				before = response.links.previous?.before;
			}

			lastUpdated = new Date();
			problem.clear('general');
			data.update((data) => {
				for (const summary of response.data?.reverse() || []) {
					data.push(summary);
				}

				return data.slice(-10);
			});
		} else {
			problem = problem.setErrorMessage(
				'An error occurred while loading events, please try again.'
			);
		}
	}

	data.subscribe((data) => {
		options.update((options) => ({
			...options,
			data
		}));
	});

	async function onPersistentEvent({
		detail
	}: CustomEvent<WebSocketMessageValue<'PersistentEventChanged'>>) {
		switch (detail.change_type) {
			case ChangeType.Added:
			case ChangeType.Saved:
				return await loadData();
			case ChangeType.Removed:
				data.update((data) => data.filter((doc) => doc.id !== detail.id));
				break;
		}
	}

	onMount(() => {
		loadData();

		document.addEventListener('PersistentEventChanged', onPersistentEvent);

		return () => {
			document.removeEventListener('PersistentEventChanged', onPersistentEvent);
		};
	});

	const dispatch = createEventDispatcher();
</script>

<table class="table table-xs">
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

<p class="text-center text-xs text-gray-700">
	Streaming events... Last updated <Time live={true} relative={true} timestamp={lastUpdated}
	></Time>
</p>

<!-- TODO: Error and loading indicators -->
