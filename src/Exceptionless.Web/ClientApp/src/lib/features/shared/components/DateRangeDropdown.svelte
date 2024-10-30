<script lang="ts">
    import * as Select from '$comp/ui/select';

    interface Props {
        value: string;
    }

    let { value = $bindable() }: Props = $props();

    const items = [
        { label: 'Last Hour', value: 'last hour' },
        { label: 'Last 24 Hours', value: 'last 24 hours' },
        { label: 'Last Week', value: 'last week' },
        { label: 'Last 30 Days', value: 'last 30 days' },
        { label: 'All Time', value: '' }
    ];

    let selected = $derived(items.find((item) => item.value === value) || items[items.length - 1]);
</script>

<Select.Root type="single" {items} bind:value>
    <Select.Trigger class="w-[135px]">
        {selected.label}
    </Select.Trigger>
    <Select.Content>
        {#each items as item (item.label)}
            <Select.Item value={item.value}>{item.label}</Select.Item>
        {/each}
    </Select.Content>
</Select.Root>
