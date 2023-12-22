<script lang="ts">
	import { writable, type Readable } from 'svelte/store';
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
	import { createEventDispatcher } from 'svelte';
	import { DEFAULT_LIMIT } from '$lib/helpers/api';
	import {
		type FetchClientResponse,
		globalFetchClient as api,
		globalLoading as loading
	} from '$api/FetchClient';
	import TableWithPaging from '$comp/table/TableWithPaging.svelte';
	import TableWithPagingFooter from '$comp/table/TableWithPagingFooter.svelte';
	import { persisted } from 'svelte-local-storage-store';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
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

	const table = createSvelteTable<SummaryModel<SummaryTemplateKeys>>(options);
	const page = writable(0);

	let response: FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;
	const parameters = writable<IGetEventsParams>({ mode });
	parameters.subscribe(async () => await loadData());
	filter.subscribe(async () => await loadData());
	time.subscribe(async () => await loadData());
	let total = 0;

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

	function onNavigateToFirstPage() {
		page.update(() => 0);
		parameters.update((params) => ({
			...params,
			before: undefined,
			after: undefined
		}));
	}

	function onPreviousPage() {
		page.update((page) => Math.max(page - 1, 0));
		parameters.update((params) => ({
			...params,
			before: response?.meta.links.next?.before,
			after: undefined
		}));
	}

	function onNextPage() {
		page.update((page) => page + 1);
		parameters.update((params) => ({
			...params,
			before: undefined,
			after: response?.meta.links.next?.after
		}));
	}

	const dispatch = createEventDispatcher();
</script>

<CustomEventMessage type="refresh" on:message={loadData}></CustomEventMessage>

<TableWithPaging
	{table}
	loading={$loading}
	error={response?.problem?.errors.general}
	page={$page}
	pageTotal={response?.data?.length || 0}
	limit={DEFAULT_LIMIT}
	{total}
	{onNavigateToFirstPage}
	{onPreviousPage}
	{onNextPage}
	on:rowclick={(event) => dispatch('rowclick', event.detail)}
>
	<slot slot="header" name="header" {table} />
	<slot
		slot="footer"
		name="footer"
		{table}
		loading={$loading}
		error={response?.problem?.errors.general}
		page={$page}
		pageTotal={response?.data?.length || 0}
		limit={DEFAULT_LIMIT}
		{total}
		{onNavigateToFirstPage}
		{onPreviousPage}
		{onNextPage}
	>
		<TableWithPagingFooter
			loading={$loading}
			error={response?.problem?.errors.general}
			page={$page}
			pageTotal={response?.data?.length || 0}
			limit={DEFAULT_LIMIT}
			{total}
			{onNavigateToFirstPage}
			{onPreviousPage}
			{onNextPage}
		></TableWithPagingFooter>
	</slot>
</TableWithPaging>
