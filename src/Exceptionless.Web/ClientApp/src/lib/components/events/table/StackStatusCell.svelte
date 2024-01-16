<script lang="ts">
	import { statuses } from '../stack';

	export let value: string;
	const status = statuses.find((status) => status.value === value);
	const Icon = status?.icon;
</script>

{#if status}
	<div class="flex w-[100px] items-center">
		{#if Icon}
			<Icon class="w-4 h-4 mr-2 text-muted-foreground" />
		{/if}
		<span>{status.label}</span>
	</div>
{/if}
