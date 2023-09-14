<script lang="ts">
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
	import WebSocketMessage from '$comp/WebSocketMessage.svelte';
	import EventsUserIdentitySummaryColumn from './EventsUserIdentitySummaryColumn.svelte';
	import ErrorMessage from '$comp/ErrorMessage.svelte';
	import Loading from '$comp/Loading.svelte';
	import Table from '$comp/table/Table.svelte';
	import { createEventDispatcher } from 'svelte';

	const defaultColumns: ColumnDef<EventSummaryModel<SummaryTemplateKeys>>[] = [
		{
			header: 'Summary',
			cell: (prop) => flexRender(Summary, { summary: prop.row.original })
		},
		{
			id: 'user',
			header: 'User',
			cell: (prop) =>
				flexRender(EventsUserIdentitySummaryColumn, { summary: prop.row.original })
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

		const response = await api.getJSON<EventSummaryModel<SummaryTemplateKeys>[]>('events', {
			params: { ...defaultParams, before }
		});

		if (response.ok) {
			lastUpdated = new Date();
			before = response.links.previous?.before;
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

	const dispatch = createEventDispatcher();

	loadData();
</script>

<WebSocketMessage type="PersistentEventChanged" on:message={onPersistentEvent}></WebSocketMessage>

<Table {table} on:rowclick={(event) => dispatch('rowclick', event.detail)}></Table>

<p class="py-2 text-center text-xs text-gray-700">
	{#if $loading}
		<Loading></Loading>
	{:else if problem.errors.general}
		<ErrorMessage message={problem.errors.general}></ErrorMessage>
	{:else}
		Streaming events... Last updated <span class="font-medium"
			><Time live={true} relative={true} timestamp={lastUpdated}></Time></span
		>
	{/if}
</p>
