<script lang="ts">
	import { createEventDispatcher } from 'svelte';
	import { writable, type Readable } from 'svelte/store';
	import { createSvelteTable } from '@tanstack/svelte-table';
	import * as DataTable from '$comp/data-table';
	import type { EventSummaryModel, IGetEventsParams, SummaryTemplateKeys } from '$lib/models/api';
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import WebSocketMessage from '$comp/messaging/WebSocketMessage.svelte';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import Loading from '$comp/Loading.svelte';
	import { ChangeType, type WebSocketMessageValue } from '$lib/models/websocket';
	import TimeAgo from '$comp/formatters/TimeAgo.svelte';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
	import Muted from '$comp/typography/Muted.svelte';
	import { getOptions } from './options';
	import SearchInput from '$comp/SearchInput.svelte';
	import { limit, onFilterInputChanged } from '$lib/stores/events';
	import { DEFAULT_LIMIT } from '$lib/helpers/api';

	export let filter: Readable<string>;

	const parameters = writable<IGetEventsParams>({ mode: 'summary', limit: $limit });
	const options = getOptions<EventSummaryModel<SummaryTemplateKeys>>(parameters, (options) => ({
		...options,
		columns: options.columns
			.filter((c) => c.id !== 'select')
			.map((c) => ({ ...c, enableSorting: false })),
		enableRowSelection: false,
		enableMultiRowSelection: false,
		manualSorting: false
	}));
	const table = createSvelteTable(options);

	let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;
	const data = writable<EventSummaryModel<SummaryTemplateKeys>[]>([]);

	let lastUpdated: Date;
	let before: string | undefined;

	parameters.subscribe(async () => await loadData());
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
				...$parameters,
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
			data,
			meta: response?.meta
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

	const dispatch = createEventDispatcher();
</script>

<CustomEventMessage type="refresh" on:message={loadData}></CustomEventMessage>
<WebSocketMessage type="PersistentEventChanged" on:message={onPersistentEvent}></WebSocketMessage>

<DataTable.Root>
	<DataTable.Toolbar {table}>
		<slot name="toolbar">
			<SearchInput
				class="h-8 w-[120px] lg:w-[350px] xl:w-[550px]"
				value={$filter}
				on:input={onFilterInputChanged}
			/>
		</slot>
	</DataTable.Toolbar>
	<DataTable.Body {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}
	></DataTable.Body>
	<Muted class="flex items-center justify-between flex-1">
		<DataTable.PageSize {table} bind:value={$limit}></DataTable.PageSize>
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
		<div></div>
	</Muted>
</DataTable.Root>
