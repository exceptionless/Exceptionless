<script lang="ts">
	import MenuDownIcon from '~icons/mdi/menu-down';
	import CopyToClipboardButton from '$comp/CopyToClipboardButton.svelte';
	import ObjectDump from '$comp/ObjectDump.svelte';

	export let canPromote: boolean = true;
	export let data: unknown;
	export let demoteTab: () => void = () => {};
	export let excludedKeys: string[] = [];
	export let isPromoted: boolean = false;
	export let promoteTab: () => void = () => {};
	export let title: string;

	let showRaw = false;

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

	let filteredData = getData(data, excludedKeys);
	let hasData = hasFilteredData(filteredData);
	let json = data ? JSON.stringify(data, null, 2) : null;

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
</script>

{#if hasData}
	<div class="flex justify-between mb-2">
		<h4 class="text-lg">{title}</h4>
		<div class="flex justify-end">
			<CopyToClipboardButton value={json}></CopyToClipboardButton>
			<div class="relative inline-block text-left pl-1">
				<div class="join">
					<button
						class="btn btn-xs btn-outline btn-neutral join-item"
						on:click|preventDefault={() => (showRaw = !showRaw)}
					>
						Toggle View
					</button>

					{#if canPromote}
						<div class="dropdown dropdown-bottom dropdown-end">
							<button
								tabindex="0"
								class="btn btn-xs btn-outline btn-neutral btn-square join-item"
								><MenuDownIcon /></button
							>
							<!-- svelte-ignore a11y-no-noninteractive-tabindex -->
							<div
								tabindex="0"
								class="dropdown-content z-[1] menu shadow bg-base-100 rounded-box w-32 p-2"
							>
								{#if !isPromoted}
									<button on:click|preventDefault={promoteTab}>
										Promote to Tab
									</button>
								{:else}
									<button on:click|preventDefault={demoteTab}>
										Demote Tab
									</button>
								{/if}
							</div>
						</div>
					{/if}
				</div>
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
