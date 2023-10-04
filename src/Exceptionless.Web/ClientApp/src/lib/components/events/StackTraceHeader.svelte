<script lang="ts">
	import type { ErrorInfo, SimpleErrorInfo } from '$lib/models/client-data';

	export let errors: (SimpleErrorInfo | ErrorInfo)[] = [];
</script>

{#each errors as error, index}
	<span class="block">
		{#if index > 0}
			--->
		{/if}
		{#if error.type}
			<span class="font-bold">{error.type}: </span>
		{/if}
		{#if error.message}
			{error.message}
		{/if}
	</span>
{/each}
