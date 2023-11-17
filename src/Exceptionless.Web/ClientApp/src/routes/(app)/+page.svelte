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
	import {
		Button,
		Card,
		Checkbox,
		CloseButton,
		Drawer,
		Dropdown,
		DropdownDivider,
		DropdownItem,
		Toggle
	} from 'flowbite-svelte';
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

<Card padding="sm" class="bg-white rounded-lg dark:bg-gray-800">
	<div class="flex justify-between items-center mb-4">
		<div>
			<h3 class="mb-2 text-xl font-bold text-gray-900 dark:text-white">Events</h3>
		</div>
	</div>

	{#if $liveMode}
		<EventsTailLogTable on:rowclick={onRowClick} {filter}>
			<div slot="header" let:table>
				<div class="flex justify-between items-center pb-4">
					<div class="w-2/4">
						<SearchInput value={$filter} onChanged={onFilterInputChanged} />
					</div>
					<div class="flex items-center space-x-2">
						<Toggle
							bind:checked={$liveMode}
							class="rounded p-2 hover:bg-gray-100 dark:hover:bg-gray-600"
							>Live</Toggle
						>
						<TableColumnPicker {table}></TableColumnPicker>
					</div>
				</div>
			</div>
		</EventsTailLogTable>
	{:else}
		<EventsTable on:rowclick={onRowClick} {filter} {time}>
			<div slot="header" let:table>
				<div class="flex justify-between items-center pb-4">
					<div class="w-2/4">
						<SearchInput value={$filter} onChanged={onFilterInputChanged} />
					</div>
					<DateRangeDropdown bind:value={$time}></DateRangeDropdown>
					<div class="flex items-center space-x-2">
						<Toggle
							bind:checked={$liveMode}
							class="rounded p-2 hover:bg-gray-100 dark:hover:bg-gray-600"
							>Live</Toggle
						>
						<TableColumnPicker {table}></TableColumnPicker>
					</div>
				</div>
			</div>
		</EventsTable>
	{/if}
</Card>

<Drawer
	backdrop={true}
	placement="right"
	leftOffset="top-16 h-screen left-0"
	transitionType="fly"
	transitionParams={transitionParamsRight}
	bind:hidden={hideDrawer}
	id="events-drawer"
>
	<div class="flex items-center">
		<h5
			id="drawer-label"
			class="inline-flex items-center mb-4 text-base font-semibold text-gray-500 dark:text-gray-400"
		>
			Info
		</h5>
		<CloseButton on:click={() => (hideDrawer = true)} class="mb-4 dark:text-white" />
	</div>
	<p class="mb-6 text-sm text-gray-500 dark:text-gray-400">
		Supercharge your hiring by taking advantage of our <a
			href="/"
			class="text-primary-600 underline dark:text-primary-500 hover:no-underline"
		>
			limited-time sale
		</a>
		for Flowbite Docs + Job Board. Unlimited access to over 190K top-ranked candidates and the #1
		design job board.
	</p>
	<div class="grid grid-cols-2 gap-4">
		<Button color="light" href="/">Learn more</Button>
		<Button href="/" class="px-4">Get access</Button>
	</div>
</Drawer>
