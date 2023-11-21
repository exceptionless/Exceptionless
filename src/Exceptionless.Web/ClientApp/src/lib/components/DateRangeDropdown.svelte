<script lang="ts">
	import * as Select from '$comp/ui/select';
	export let value: string;
</script>

<Select.Root bind:selected={value}>
	<Select.Trigger class="w-[180px]">
		<Select.Value placeholder="" />
	</Select.Trigger>
	<Select.Content>
		<Select.Item value="last hour">Last Hour</Select.Item>
		<Select.Item value="last 24 hours">Last 24 Hours</Select.Item>
		<Select.Item value="last week">Last Week</Select.Item>
		<Select.Item value="last 30 days">Last 30 Days</Select.Item>
		<Select.Item value="">All Time</Select.Item>
	</Select.Content>
</Select.Root>
