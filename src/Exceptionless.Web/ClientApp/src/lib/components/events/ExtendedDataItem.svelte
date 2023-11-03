<script lang="ts">
	import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';
	import ObjectDump from '$comp/ObjectDump.svelte';

	export let canPromote: boolean = true;
	export let data: Record<string, unknown>;
	export let demoteTab: () => void = () => {};
	export let excludedKeys: string[] = [];
	export let isPromoted: boolean = false;
	export let promoteTab: () => void = () => {};
	export let title: string;

	let showRaw = false;

	function getData(data: Record<string, unknown>, exclusions: string[]): Record<string, unknown> {
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

	let filteredData = getData(data, excludedKeys);
	let hasFilteredData =
		typeof filteredData !== 'undefined' && Object.keys(filteredData).length > 0;
	let json = data ? JSON.stringify(data, null, 2) : null;
</script>

{#if hasFilteredData}
	<div class="flex justify-between mt-4 mb-2">
		<h4 class="text-lg">{title}</h4>
		<div class="flex justify-end">
			<CopyToClipboardButton value={json}></CopyToClipboardButton>
			<div class="relative inline-block text-left pl-1">
				<button
					type="button"
					class="btn btn-xs btn-outline btn-neutral"
					on:click|preventDefault={() => (showRaw = !showRaw)}
				>
					Toggle View
				</button>

				{#if canPromote}
					<div
						class="origin-top-right absolute right-0 mt-2 w-56 rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5"
						role="menu"
						aria-orientation="vertical"
						aria-labelledby="options-menu"
					>
						<div class="py-1" role="none">
							{#if !isPromoted}
								<button
									class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 hover:text-gray-900"
									role="menuitem"
									on:click|preventDefault={promoteTab}
								>
									Promote to Tab
								</button>
							{:else}
								<button
									class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 hover:text-gray-900"
									role="menuitem"
									on:click|preventDefault={demoteTab}
								>
									Demote Tab
								</button>
							{/if}
						</div>
					</div>
				{/if}
			</div>
		</div>
	</div>

	{#if showRaw}
		<pre
			class="whitespace-pre-wrap break-words overflow-auto p-2 mt-2 border border-base-300 text-xs"><code
				>{json}</code
			></pre>
	{:else}
		<ObjectDump value={filteredData} />
	{/if}
{/if}
