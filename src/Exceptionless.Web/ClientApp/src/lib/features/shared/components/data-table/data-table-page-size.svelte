<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import * as Select from '$comp/ui/select';

    interface Props {
        table: Table<TData>;
        value: number;
    }

    let { table, value = $bindable() }: Props = $props();

    type Item = { label: string; value: string };
    const items: Item[] = [
        { label: '5', value: '5' },
        { label: '10', value: '10' },
        { label: '20', value: '20' },
        { label: '30', value: '30' },
        { label: '40', value: '40' },
        { label: '50', value: '50' }
    ];

    let valueString = $derived(value + '');
    let selected = $derived((items.find((item) => item.value === valueString) || items[0]) as Item);

    function onValueChange(newValue: string) {
        value = Number(newValue);
        table.setPageSize(Number(newValue));
    }
</script>

<div class="flex items-center gap-2 min-w-0">
    <p class="text-sm font-medium truncate hidden sm:inline">Rows per page</p>
    <Select.Root type="single" {items} value={valueString} {onValueChange}>
        <Select.Trigger class="h-8 w-[70px] min-w-[46px]">
            {selected.label}
        </Select.Trigger>
        <Select.Content>
            {#each items as item (item.value)}
                <Select.Item value={item.value}>{item.label}</Select.Item>
            {/each}
        </Select.Content>
    </Select.Root>
</div>
