<script lang="ts">
	import { toast } from 'svoast';
	import { mutateDemoteTab } from "$api/queries/projects";
	import type { PersistentEvent } from '$lib/models/api';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import { createEventDispatcher } from "svelte";

	export let event: PersistentEvent;
	export let title: string;

	const dispatch = createEventDispatcher();

    const demoteTab = mutateDemoteTab(event.project_id ?? '');
	demoteTab.subscribe((response) => {
        if (response.isError) {
		    toast.error(`An error occurred demoting tab ${response.variables.name}`);
        } else if (response.isSuccess) {
            dispatch('demoted', response.variables.name);
        }
	});

	function onDemote({ detail }: CustomEvent<string>): void {
		$demoteTab.mutate({ name: detail });
	}
</script>

<ExtendedDataItem {title} isPromoted={true} data={event.data?.[title]} on:demote={onDemote}
></ExtendedDataItem>
