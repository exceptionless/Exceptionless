<script lang="ts">
	import { StackStatus } from '$lib/models/api.generated';
	import {
		QuestionMarkCircled,
		Circle,
		Stopwatch,
		CheckCircled,
		CrossCircled
	} from 'radix-icons-svelte';

	const statuses = [
		{
			value: StackStatus.Open,
			label: 'Open',
			icon: QuestionMarkCircled
		},
		{
			value: StackStatus.Fixed,
			label: 'Fixed',
			icon: Circle
		},
		{
			value: StackStatus.Regressed,
			label: 'Regressed',
			icon: Stopwatch
		},
		{
			value: StackStatus.Snoozed,
			label: 'Snoozed',
			icon: CheckCircled
		},
		{
			value: StackStatus.Ignored,
			label: 'Ignored',
			icon: CrossCircled
		},
		{
			value: StackStatus.Discarded,
			label: 'Discarded',
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
