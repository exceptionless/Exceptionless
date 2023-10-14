<script lang="ts">
	import { writable, type Readable } from 'svelte/store';
	import {
		createSvelteTable,
		flexRender,
		getCoreRowModel,
		type ColumnDef,
		type TableOptions,
		type Updater,
		type VisibilityState
	} from '@tanstack/svelte-table';
	import type { EventSummaryModel, IGetEventsParams, SummaryTemplateKeys } from '$lib/models/api';
	import Summary from '$comp/events/summary/Summary.svelte';
	import { nameof } from '$lib/utils';
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import WebSocketMessage from '$comp/messaging/WebSocketMessage.svelte';
	import EventsUserIdentitySummaryColumn from './EventsUserIdentitySummaryColumn.svelte';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import Loading from '$comp/Loading.svelte';
	import Table from '$comp/table/Table.svelte';
	import { createEventDispatcher } from 'svelte';
	import { persisted } from 'svelte-local-storage-store';
	import { ChangeType, type WebSocketMessageValue } from '$lib/models/websocket';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';

	export let filter: Readable<string>;

	const defaultColumns: ColumnDef<EventSummaryModel<SummaryTemplateKeys>>[] = [
		{
			header: 'Summary',
			enableHiding: false,
			cell: (prop) => flexRender(Summary, { summary: prop.row.original })
		},
		{
			id: 'user',
			header: 'User',
			meta: {
				class: 'w-28'
			},
			cell: (prop) =>
				flexRender(EventsUserIdentitySummaryColumn, { summary: prop.row.original })
		},
		{
			id: 'date',
			header: 'Date',
			meta: {
				class: 'w-36'
			},
			accessorKey: nameof<EventSummaryModel<SummaryTemplateKeys>>('date'),
			cell: (prop) => flexRender(TimeAgo, { value: prop.getValue() })
		}
	];

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

	const options = writable<TableOptions<EventSummaryModel<SummaryTemplateKeys>>>({
		data: [],
		columns: defaultColumns,
		enableSorting: false,
		state: {
			columnVisibility: $columnVisibility
		},
		onColumnVisibilityChange: setColumnVisibility,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow) => originalRow.id
	});

	const table = createSvelteTable<EventSummaryModel<SummaryTemplateKeys>>(options);

	let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;
	const data = writable<EventSummaryModel<SummaryTemplateKeys>[]>([]);

	let lastUpdated: Date;
	let before: string | undefined;

	const defaultParams: IGetEventsParams = { mode: 'summary' };
	filter.subscribe(async () => {
		before = undefined;
		data.set([]);
		await loadData();
	});

	async function loadData() {
		if ($loading) {
			return;
		}

		response = await api.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
			params: {
				...defaultParams,
				filter: $filter,
				before
			}
		});

		if (response.ok) {
			lastUpdated = new Date();
			before = response.links.previous?.before;

			data.update((data) => {
				for (const summary of response.data?.reverse() || []) {
					data.push(summary);
				}

				return data.slice(-10);
			});
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

	loadData();

	const dispatch = createEventDispatcher();
</script>

<WebSocketMessage type="PersistentEventChanged" on:message={onPersistentEvent}></WebSocketMessage>

<Table {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}>
	<slot slot="header" name="header" {table} />
	<slot
		slot="footer"
		name="footer"
		{table}
		error={response?.problem?.errors.general}
		{loading}
		{lastUpdated}
	>
		<p class="py-2 text-center text-xs text-gray-700">
			{#if $loading}
				<Loading></Loading>
			{:else if response?.problem?.errors.general}
				<ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
			{:else}
				Streaming events... Last updated <span class="font-medium"
					><TimeAgo value={lastUpdated}></TimeAgo></span
				>
			{/if}
		</p>
	</slot>
</Table>
