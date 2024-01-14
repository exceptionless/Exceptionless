<script lang="ts">
	import { writable, type Readable } from 'svelte/store';
	import {
		createSvelteTable,
		getCoreRowModel,
		type TableOptions,
		type Updater,
		type VisibilityState
	} from '@tanstack/svelte-table';
	import type { EventSummaryModel, IGetEventsParams, SummaryTemplateKeys } from '$lib/models/api';
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import WebSocketMessage from '$comp/messaging/WebSocketMessage.svelte';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import Loading from '$comp/Loading.svelte';
	import Table from '$comp/table/Table.svelte';
	import { createEventDispatcher } from 'svelte';
	import { persisted } from 'svelte-local-storage-store';
	import { ChangeType, type WebSocketMessageValue } from '$lib/models/websocket';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
	import Muted from '$comp/typography/Muted.svelte';
	import { getColumns } from './options';

	export let filter: Readable<string>;

	const columns = getColumns<EventSummaryModel<SummaryTemplateKeys>>('summary');
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
		columns,
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
			before = response.meta.links.previous?.before;

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

<CustomEventMessage type="refresh" on:message={loadData}></CustomEventMessage>
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
		<Muted class="py-2 text-center">
			{#if $loading}
				<Loading></Loading>
			{:else if response?.problem?.errors.general}
				<ErrorMessage message={response?.problem?.errors.general}></ErrorMessage>
			{:else}
				Streaming events... Last updated <span class="font-medium"
					><TimeAgo value={lastUpdated}></TimeAgo></span
				>
			{/if}
		</Muted>
	</slot>
</Table>
