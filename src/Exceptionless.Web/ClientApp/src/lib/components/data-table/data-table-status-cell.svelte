<script lang="ts">
	import {
		QuestionMarkCircled,
		Circle,
		Stopwatch,
		CheckCircled,
		CrossCircled
	} from 'radix-icons-svelte';

	const statuses = [
		{
			value: 'backlog',
			label: 'Backlog',
			icon: QuestionMarkCircled
		},
		{
			value: 'todo',
			label: 'Todo',
			icon: Circle
		},
		{
			value: 'in progress',
			label: 'In Progress',
			icon: Stopwatch
		},
		{
			value: 'done',
			label: 'Done',
			icon: CheckCircled
		},
		{
			value: 'canceled',
			label: 'Canceled',
			icon: CrossCircled
		}
	];

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
