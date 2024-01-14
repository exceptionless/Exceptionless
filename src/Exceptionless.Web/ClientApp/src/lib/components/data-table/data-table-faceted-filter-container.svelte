<script lang="ts">
	import { type Writable, derived } from 'svelte/store';

	import { Button } from '$comp/ui/button';
	import { Cross2 } from 'radix-icons-svelte';

	export let filterValues: Writable<Record<string, string[]>>;

	const showReset = derived(filterValues, ($filterValues) => {
		return Object.values($filterValues).some((v) => v.length > 0);
	});

	function resetFilterValues() {
		filterValues.update((values) => {
			Object.keys(values).forEach((key) => {
				values[key] = [];
			});

			return values;
		});
	}
</script>

<slot />

{#if $showReset}
	<Button on:click={() => resetFilterValues()} variant="ghost" class="h-8 px-2 lg:px-3">
		Reset
		<Cross2 class="w-4 h-4 ml-2" />
	</Button>
{/if}
