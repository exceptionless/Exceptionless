<script lang="ts">
	import { writable, type Readable, type Writable } from 'svelte/store';
	import {
		createSvelteTable,
		getCoreRowModel,
		type TableOptions,
		type Updater,
		type VisibilityState,
		type ColumnSort
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
	import { getColumns } from './options';

	export let mode: GetEventsMode = 'summary';
	export let filter: Readable<string>;
	export let time: Readable<string>;

	const columns = getColumns(mode);
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

	let sorting: ColumnSort[] = [
		{
			id: 'date',
			desc: true
		}
	];
	const setSorting = (updaterOrValue: Updater<ColumnSort[]>) => {
		if (updaterOrValue instanceof Function) {
			sorting = updaterOrValue(sorting);
		} else {
			sorting = updaterOrValue;
		}

		options.update((old) => ({
			...old,
			state: {
				...old.state,
				sorting
			}
		}));

		parameters.update((params) => ({
			...params,
			before: undefined,
			after: undefined,
			sort:
				sorting.length > 0
					? sorting.map((sort) => `${sort.desc ? '-' : ''}${sort.id}`).join(',')
					: undefined
		}));
	};

	const options = writable<TableOptions<SummaryModel<SummaryTemplateKeys>>>({
		columns,
		data: [],
		enableSortingRemoval: false,
		manualSorting: true,
		state: {
			columnVisibility: $columnVisibility,
			sorting
		},
		onColumnVisibilityChange: setColumnVisibility,
		onSortingChange: setSorting,
		getCoreRowModel: getCoreRowModel(),
		getRowId: (originalRow) => originalRow.id
	});

	// TODO: https://tanstack.com/table/v8/docs/api/features/pagination#setpagesize

	const table = createSvelteTable<SummaryModel<SummaryTemplateKeys>>(options);

	let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;
	const parameters = writable<IGetEventsParams>({ mode });
	parameters.subscribe(async () => await loadData());
	filter.subscribe(async () => await loadData());
	time.subscribe(async () => await loadData());
	let total = 0;
	console.log(total);

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
			options.update((options) => ({
				...options,
				data: response.data || []
			}));
			if (response.meta?.total) total = response.meta.total as number;
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

	const priorities = [
		{
			label: 'Low',
			value: 'low',
			icon: ArrowDown
		},
		{
			label: 'Medium',
			value: 'medium',
			icon: ArrowRight
		},
		{
			label: 'High',
			value: 'high',
			icon: ArrowUp
		}
	];

	const filterValue: Writable<string> = writable('');
	const filterValues: Writable<{
		status: string[];
		priority: string[];
	}> = writable({
		status: [],
		priority: []
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
		<DataTable.FacetedFilter
			bind:filterValues={$filterValues.priority}
			title="Priority"
			options={priorities}
		/>
		{#if showReset}
			<Button
				on:click={() => {
					$filterValues.status = [];
					$filterValues.priority = [];
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
