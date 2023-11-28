<script lang="ts">
	import ArrowDownIcon from '~icons/mdi/arrow-down';
	import ArrowUpIcon from '~icons/mdi/arrow-up';
	import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';
	import ObjectDump from '$comp/ObjectDump.svelte';
	import { createEventDispatcher } from 'svelte';
	import { Button } from '$comp/ui/button';

	export let title: string;
	export let data: unknown;
	export let canPromote: boolean = true;
	export let isPromoted: boolean = false;
	export let excludedKeys: string[] = [];

	function getData(data: unknown, exclusions: string[]): unknown {
		if (typeof data !== 'object' || !(data instanceof Object)) {
			return data;
		}

		return Object.entries(data)
			.filter(([key]) => !exclusions.includes(key))
			.sort(([keyA], [keyB]) => keyA.localeCompare(keyB))
			.reduce((acc: Record<string, unknown>, [key, value]) => {
				acc[key] = value;
				return acc;
			}, {});
	}

	function hasFilteredData(data: unknown): boolean {
		if (data === undefined || data === null) {
			return false;
		}

		if (Array.isArray(data)) {
			return data.length > 0;
		}

		if (Object.prototype.toString.call(data) === '[object Object]') {
			return Object.keys(data).length > 0;
		}

		return true;
	}

	function onPromote(e: Event) {
		e.preventDefault();
		dispatch('promote', title);
	}

	function onDemote(e: Event) {
		e.preventDefault();
		dispatch('demote', title);
	}

	function onToggleView(e: Event) {
		e.preventDefault();
		showRaw = !showRaw;
	}

	let showRaw = false;
	let filteredData = getData(data, excludedKeys);
	let hasData = hasFilteredData(filteredData);
	let json = data ? JSON.stringify(data, null, 2) : null;
	const dispatch = createEventDispatcher();
</script>

{#if hasData}
	<div class="flex justify-between">
		<h4 class="text-lg mb-2">{title}</h4>
		<div class="flex justify-end gap-x-1">
			<Button variant="outline" on:click={onToggleView}>Toggle View</Button>

			<CopyToClipboardButton value={json}></CopyToClipboardButton>

			{#if canPromote}
				{#if !isPromoted}
					<Button size="icon" on:click={onPromote} title="Promote to Tab"
						><ArrowUpIcon /></Button
					>
				{:else}
					<Button size="icon" on:click={onDemote} title="Demote Tab"
						><ArrowDownIcon /></Button
					>
				{/if}
			{/if}
		</div>
	</div>

	{#if showRaw}
		<pre class="whitespace-pre-wrap break-words overflow-auto p-2 text-xs"><code>{json}</code
			></pre>
	{:else}
		<ObjectDump value={filteredData} />
	{/if}
{/if}
