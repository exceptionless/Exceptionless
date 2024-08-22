<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';
    import type { Selected } from 'bits-ui';

    import * as Select from '$comp/ui/select';

    interface Props {
        defaultValue?: number;
        table: Table<TData>;
        value: number;
    }

    let { defaultValue = 10, table, value = $bindable() }: Props = $props();

    const items = [
        { label: '5', value: 5 },
        { label: '10', value: 10 },
        { label: '20', value: 20 },
        { label: '30', value: 30 },
        { label: '40', value: 40 },
        { label: '50', value: 50 }
    ];

    let selected = $state(items.find((item) => item.value === value) || items[0]);
    function onSelectedChange(selected: Selected<number> | undefined) {
        const newValue = selected?.value ?? defaultValue;
        if (newValue === value) {
            return;
        }

        value = newValue;
        table.setPageSize(newValue);
    }
</script>

<div class="flex items-center space-x-2">
    <p class="text-sm font-medium">Rows per page</p>
    <Select.Root bind:selected {items} {onSelectedChange}>
        <Select.Trigger class="h-8 w-[70px]">
            <Select.Value placeholder="Select page size" />
        </Select.Trigger>
        <Select.Content>
            {#each items as item (item.value)}
                <Select.Item value={item.value}>{item.label}</Select.Item>
            {/each}
        </Select.Content>
    </Select.Root>
</div>
