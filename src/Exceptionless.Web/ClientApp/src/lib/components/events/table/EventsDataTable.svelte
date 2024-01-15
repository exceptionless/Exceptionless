<script lang="ts">
	import { createSvelteTable } from '@tanstack/svelte-table';
	import { createEventDispatcher } from 'svelte';
	import { writable, type Readable } from 'svelte/store';

	import type { EventSummaryModel, IGetEventsParams, SummaryTemplateKeys } from '$lib/models/api';
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';

	import * as DataTable from '$comp/data-table';
	import { getOptions } from './options';
	import { DEFAULT_LIMIT } from '$lib/helpers/api';
	import SearchInput from '$comp/SearchInput.svelte';
	import { limit, onFilterInputChanged } from '$lib/stores/events';

	export let filter: Readable<string>;
	export let time: Readable<string>;

	const parameters = writable<IGetEventsParams>({ mode: 'summary', limit: $limit });
	const options = getOptions<EventSummaryModel<SummaryTemplateKeys>>(parameters);
	const table = createSvelteTable(options);

	let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;
	parameters.subscribe(async () => await loadData());
	filter.subscribe(async () => await loadData());
	time.subscribe(async () => await loadData());

	async function loadData() {
		if ($loading) {
			return;
		}

		response = await api.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
			params: {
				...$parameters,
				filter: $filter,
				time: $time
			}
		});

		if (response.ok) {
			let total = (response.meta?.total as number) ?? 0;
			options.update((options) => ({
				...options,
				data: response.data || [],
				page: $parameters.page ?? 0,
				pageCount: Math.ceil(total / ($parameters.limit ?? DEFAULT_LIMIT)),
				meta: response.meta
			}));

			$table.resetRowSelection();
		}
	}

	const dispatch = createEventDispatcher();
</script>

<CustomEventMessage type="refresh" on:message={loadData}></CustomEventMessage>

<DataTable.Root>
	<DataTable.Toolbar {table}>
		<slot name="toolbar">
			<SearchInput
				class="h-8 w-[150px] lg:w-[250px]"
				value={$filter}
				on:input={onFilterInputChanged}
			/>
		</slot>
	</DataTable.Toolbar>
	<DataTable.Body {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}
	></DataTable.Body>
	<DataTable.Pagination {table}>
		<DataTable.PageSize {table} bind:value={$limit}></DataTable.PageSize>
	</DataTable.Pagination>
</DataTable.Root>
