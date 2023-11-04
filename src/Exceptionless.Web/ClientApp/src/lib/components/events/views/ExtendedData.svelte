<script lang="ts">
	import { toast } from 'svoast';
	import type { PersistentEvent, ViewProject } from '$lib/models/api';
	import ExtendedDataItem from '../ExtendedDataItem.svelte';
	import { getExtendedDataItems } from '$lib/helpers/persistent-event';
	import { mutatePromoteTab } from '$api/queries/projects';

	export let event: PersistentEvent;
	export let project: ViewProject | undefined;

	const items = getExtendedDataItems(event, project);

	const promoteTab = mutatePromoteTab(event.project_id ?? '');
	promoteTab.subscribe((response) => {
        if (response.isError) {
		    toast.error(`An error occurred promoting tab ${response.variables.name}`);
        }
	});

	function onPromote({ detail }: CustomEvent<string>): void {
		$promoteTab.mutate({ name: detail });
	}
</script>

<div class="space-y-4">
	{#each items as { title, promoted, data }}
		{#if promoted === false}
			<div data-id={title}>
				<ExtendedDataItem {title} {data} on:promote={onPromote}></ExtendedDataItem>
			</div>
		{/if}
	{/each}
</div>
