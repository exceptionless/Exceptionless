<script lang="ts">
	import EventsTable from '$comp/events/table/EventsTable.svelte';
	import EventsTailLogTable from '$comp/events/table/EventsTailLogTable.svelte';
	import TableColumnPicker from '$comp/table/TableColumnPicker.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import { persisted } from 'svelte-local-storage-store';
	import EventsDrawer from '$comp/events/EventsDrawer.svelte';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
	import {
		type IFilter,
		updateFilters,
		parseFilter,
		FilterSerializer,
		toFilter
	} from '$comp/filters/filters';
	import { derived } from 'svelte/store';

	import { sineIn } from 'svelte/easing';
	import SearchInput from '$comp/SearchInput.svelte';
	import DateRangeDropdown from '$comp/DateRangeDropdown.svelte';

	let liveMode = persisted<boolean>('live', true);
	let hideDrawer = true;

	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		hideDrawer = false;
	}

	let time = persisted<string>('time', '');

	const filters = persisted<IFilter[]>('filters', [], { serializer: new FilterSerializer() });
	let filter = derived(filters, ($filters) => toFilter($filters));
	function onFilterChanged({ detail }: CustomEvent<IFilter>): void {
		filters.set(updateFilters($filters, detail));
	}

	let parseFiltersDebounceTimer: ReturnType<typeof setTimeout>;
	function onFilterInputChanged(event: Event) {
		clearTimeout(parseFiltersDebounceTimer);
		parseFiltersDebounceTimer = setTimeout(() => {
			const { value } = event.target as HTMLInputElement;
			filters.set(parseFilter($filters, value));
		}, 500);
	}

	let transitionParamsRight = {
		x: 320,
		duration: 200,
		easing: sineIn
	};
</script>

<svelte:head>
	<title>Exceptionless</title>
</svelte:head>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

Test
