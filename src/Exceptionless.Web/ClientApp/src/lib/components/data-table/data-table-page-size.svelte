<script lang="ts">
    import type { Table } from '@tanstack/svelte-table';
    import type { Selected } from 'bits-ui';

    import * as Select from '$comp/ui/select';

    type TData = $$Generic;
    export let table: Table<TData>;

    export let value: number;
    export let defaultValue: number = 10;

    const items = [
        { value: 5, label: '5' },
        { value: 10, label: '10' },
        { value: 20, label: '20' },
        { value: 30, label: '30' },
        { value: 40, label: '40' },
        { value: 50, label: '50' }
    ];

    let selected = items.find((item) => item.value === value) || items[0];
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
    <Select.Root {items} bind:selected {onSelectedChange}>
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
