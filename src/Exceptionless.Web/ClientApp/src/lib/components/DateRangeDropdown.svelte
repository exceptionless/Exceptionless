<script lang="ts">
	import * as Select from '$comp/ui/select';
	import type { Selected } from 'bits-ui';

	export let value: string;

	const items = [
		{ value: 'last hour', label: 'Last Hour' },
		{ value: 'last 24 hours', label: 'Last 24 Hours' },
		{ value: 'last week', label: 'Last Week' },
		{ value: 'last 30 days', label: 'Last 30 Days' },
		{ value: '', label: 'All Time' }
	];

	let selected = items.find((item) => item.value === value) || items[items.length - 1];

	function onSelectedChange(selected: Selected<string> | undefined) {
		value = selected?.value ?? '';
	}
</script>

<Select.Root {items} bind:selected {onSelectedChange}>
	<Select.Trigger class="w-[180px]">
		<Select.Value placeholder="" />
	</Select.Trigger>
	<Select.Content>
		{#each items as item}
			<Select.Item value={item.value}>{item.label}</Select.Item>
		{/each}
	</Select.Content>
</Select.Root>
