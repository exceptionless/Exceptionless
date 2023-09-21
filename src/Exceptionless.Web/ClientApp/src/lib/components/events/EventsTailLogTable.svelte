<script lang="ts">
	import { writable, type Readable } from 'svelte/store';
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
		type IGetEventsParams,
		type SummaryTemplateKeys,
		type WebSocketMessageValue
	} from '$lib/models/api';
	import Summary from '$comp/events/summary/Summary.svelte';
	import { nameof } from '$lib/utils';
	import { FetchClient, type FetchClientResponse } from '$api/FetchClient';
	import WebSocketMessage from '$comp/WebSocketMessage.svelte';
	import EventsUserIdentitySummaryColumn from './EventsUserIdentitySummaryColumn.svelte';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import Loading from '$comp/Loading.svelte';
	import Table from '$comp/table/Table.svelte';
	import { createEventDispatcher } from 'svelte';

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
		enableSorting: false,
		state: {
			columnVisibility
		},
		onColumnVisibilityChange: setColumnVisibility,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow, _, __) => originalRow.id
	});

	const table = createSvelteTable<EventSummaryModel<SummaryTemplateKeys>>(options);

	const api = new FetchClient();
	const loading = api.loading;
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
	<div slot="header">
		<slot name="header" {table} problem={response?.problem} />
	</div>
	<div slot="footer">
		<slot name="footer" {table} problem={response?.problem} />
	</div>
</Table>

<p class="py-2 text-center text-xs text-gray-700">
	{#if $loading}
		<Loading></Loading>
	{:else if response?.problem?.errors.general}
		<ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
	{:else}
		Streaming events... Last updated <span class="font-medium"
			><Time live={true} relative={true} timestamp={lastUpdated}></Time></span
		>
	{/if}
</p>
