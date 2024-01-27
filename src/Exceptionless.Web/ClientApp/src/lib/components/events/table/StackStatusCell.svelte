<script lang="ts">
	import { stackStatuses } from '../options';

	export let value: string;
	const status = stackStatuses.find((status) => status.value === value);
</script>

{#if status}
	<div class="flex w-[100px] items-center">
		<span>{status.label}</span>
	</div>
{/if}
