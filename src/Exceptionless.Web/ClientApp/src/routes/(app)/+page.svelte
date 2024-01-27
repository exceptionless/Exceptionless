<script lang="ts">
	import * as Card from '$comp/ui/card';
	import * as DataTable from '$comp/data-table';
	import * as Sheet from '$comp/ui/sheet';
	import SearchInput from '$comp/SearchInput.svelte';
	import PieChartCard from '$comp/events/cards/pie-chart-card.svelte';

	import EventsDataTable from '$comp/events/table/EventsDataTable.svelte';
	import EventsDrawer from '$comp/events/EventsDrawer.svelte';
	import type { SummaryModel, SummaryTemplateKeys } from '$lib/models/api';
	import { eventTypes, stackStatuses } from '$comp/events/options';
	import CustomEventMessage from '$comp/messaging/CustomEventMessage.svelte';
	import {
		filter,
		filterValues,
		filterWithFaceted,
		onFacetValuesChanged,
		onFilterChanged,
		onFilterInputChanged,
		resetFilterValues,
		time
	} from '$lib/stores/events';
	import DateRangeDropdown from '$comp/DateRangeDropdown.svelte';

	let selectedEventId: string | null = null;
	function onRowClick({ detail }: CustomEvent<SummaryModel<SummaryTemplateKeys>>) {
		selectedEventId = detail.id;
	}
</script>

<svelte:head>
	<title>Event Dashboard - Exceptionless</title>
</svelte:head>

<CustomEventMessage type="filter" on:message={onFilterChanged}></CustomEventMessage>

<div class="flex flex-col space-y-4">
	<Card.Root>
		<Card.Title tag="h2" class="p-6 pb-4 text-2xl">Events</Card.Title>
		<Card.Content>
			<EventsDataTable filter={filterWithFaceted} {time} on:rowclick={onRowClick}>
				<svelte:fragment slot="toolbar">
					<SearchInput
						class="h-8 w-80 lg:w-[350px] xl:w-[550px]"
						value={$filter}
						on:input={onFilterInputChanged}
					/>

					<DataTable.FacetedFilterContainer {filterValues} {resetFilterValues}>
						<DataTable.FacetedFilter
							title="Status"
							key="status"
							values={$filterValues.status}
							options={stackStatuses}
							onValueChange={onFacetValuesChanged}
						></DataTable.FacetedFilter>

						<DataTable.FacetedFilter
							title="Type"
							key="type"
							values={$filterValues.type}
							options={eventTypes}
							onValueChange={onFacetValuesChanged}
						></DataTable.FacetedFilter>
					</DataTable.FacetedFilterContainer>

					<DateRangeDropdown bind:value={$time}></DateRangeDropdown>
				</svelte:fragment>
			</EventsDataTable>
		</Card.Content>
	</Card.Root>

	<PieChartCard title="Status"></PieChartCard>
</div>

<Sheet.Root open={!!selectedEventId} onOpenChange={() => (selectedEventId = null)}>
	<Sheet.Content class="w-full overflow-y-auto sm:max-w-full md:w-5/6">
		<Sheet.Header>
			<Sheet.Title>Event Details</Sheet.Title>
		</Sheet.Header>
		<EventsDrawer id={selectedEventId || ''}></EventsDrawer>
	</Sheet.Content>
</Sheet.Root>
