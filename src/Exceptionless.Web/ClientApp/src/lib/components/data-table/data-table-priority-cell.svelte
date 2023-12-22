<script lang="ts">
	import { ArrowDown, ArrowRight, ArrowUp } from 'radix-icons-svelte';

	const priorities = [
		{
			label: 'Low',
			value: 'low',
			icon: ArrowDown
		},
		{
			label: 'Medium',
			value: 'medium',
			icon: ArrowRight
		},
		{
			label: 'High',
			value: 'high',
			icon: ArrowUp
		}
	];

	export let value: string;
	const priority = priorities.find((priority) => priority.value === value);
	const Icon = priority?.icon;
</script>

{#if priority}
	<div class="flex items-center">
		{#if Icon}
			<Icon class="w-4 h-4 mr-2 text-muted-foreground" />
		{/if}
		<span>{priority.label}</span>
	</div>
{/if}
