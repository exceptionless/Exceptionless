<script lang="ts">
	import { writable, type Readable, type Writable } from 'svelte/store';
	import {
		createSvelteTable,
		getCoreRowModel,
		type TableOptions,
		type Updater,
		type VisibilityState,
		type ColumnSort,
		type PaginationState
	} from '@tanstack/svelte-table';
	import type {
		EventSummaryModel,
		GetEventsMode,
		IGetEventsParams,
		SummaryModel,
		SummaryTemplateKeys
	} from '$lib/models/api';
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import { persisted } from 'svelte-local-storage-store';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';

	import * as DataTable from '$comp/data-table';
	import { Button } from '$comp/ui/button';
	import {
		ArrowDown,
		ArrowRight,
		ArrowUp,
		CheckCircled,
		Circle,
		Cross2,
		CrossCircled,
		QuestionMarkCircled,
		Stopwatch
	} from 'radix-icons-svelte';
	import { Input } from '$comp/ui/input';
	import { StackStatus } from '$lib/models/api.generated';
	import { getOptions } from './options';
	import { DEFAULT_LIMIT } from '$lib/helpers/api';

	export let mode: GetEventsMode = 'summary';
	export let filter: Readable<string>;
	export let time: Readable<string>;

	const parameters = writable<IGetEventsParams>({ mode });
	const options = getOptions(parameters);
	const table = createSvelteTable<SummaryModel<SummaryTemplateKeys>>(options);

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
				mode: 'summary',
				filter: $filter,
				time: $time,
				...$parameters
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
		}
	}

	const statuses = [
		{
			value: StackStatus.Open,
			label: 'Open',
			icon: QuestionMarkCircled
		},
		{
			value: StackStatus.Fixed,
			label: 'Fixed',
			icon: Circle
		},
		{
			value: StackStatus.Regressed,
			label: 'Regressed',
			icon: Stopwatch
		},
		{
			value: StackStatus.Snoozed,
			label: 'Snoozed',
			icon: CheckCircled
		},
		{
			value: StackStatus.Ignored,
			label: 'Ignored',
			icon: CrossCircled
		},
		{
			value: StackStatus.Discarded,
			label: 'Discarded',
			icon: CrossCircled
		}
	];

	const filterValue: Writable<string> = writable('');
	const filterValues: Writable<{
		status: string[];
	}> = writable({
		status: []
	});

	$: showReset = Object.values($filterValues).some((v) => v.length > 0);
</script>

<CustomEventMessage type="refresh" on:message={loadData}></CustomEventMessage>

<DataTable.Root>
	<DataTable.Toolbar {table}>
		<Input
			placeholder="Filter tasks..."
			class="h-8 w-[150px] lg:w-[250px]"
			type="text"
			bind:value={$filterValue}
		/>

		<DataTable.FacetedFilter
			bind:filterValues={$filterValues.status}
			title="Status"
			options={statuses}
		/>

		{#if showReset}
			<Button
				on:click={() => {
					$filterValues.status = [];
				}}
				variant="ghost"
				class="h-8 px-2 lg:px-3"
			>
				Reset
				<Cross2 class="w-4 h-4 ml-2" />
			</Button>
		{/if}
	</DataTable.Toolbar>
	<DataTable.Body {table}></DataTable.Body>
	<DataTable.Pagination {table}></DataTable.Pagination>
</DataTable.Root>
