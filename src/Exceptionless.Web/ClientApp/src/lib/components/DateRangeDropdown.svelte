<script lang="ts">
    import type { Selected } from 'bits-ui';

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

    function onSelectedChange(selected: Selected<string> | undefined) {
        const newValue = selected?.value ?? '';
        if (newValue !== value) {
            value = newValue;
        }
    }
</script>

<Select.Root {items} {onSelectedChange} {selected}>
    <Select.Trigger class="w-[135px]">
        <Select.Value placeholder="" />
    </Select.Trigger>
    <Select.Content>
        {#each items as item (item.label)}
            <Select.Item value={item.value}>{item.label}</Select.Item>
        {/each}
    </Select.Content>
</Select.Root>
