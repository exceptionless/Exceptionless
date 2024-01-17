<script lang="ts">
	import { statuses } from '../stack';

	export let value: string;
	const status = statuses.find((status) => status.value === value);
</script>

{#if status}
	<div class="flex w-[100px] items-center">
		<span>{status.label}</span>
	</div>
{/if}
