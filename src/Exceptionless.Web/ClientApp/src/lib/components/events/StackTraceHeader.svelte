<script lang="ts">
	import type { ErrorInfo, SimpleErrorInfo } from '$lib/models/client-data';

	export let errors: (SimpleErrorInfo | ErrorInfo)[] = [];
</script>

{#each errors as error, index}<div>
		{#if index > 0}<span class="ml-6">---> </span>{/if}{#if error.type}<span
				class="font-bold mr-1">{error.type}:</span
			>{/if}{#if error.message}{error.message}{/if}
	</div>
{/each}
