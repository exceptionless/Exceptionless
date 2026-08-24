<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { RowData, StockFeatures, Table } from '@tanstack/svelte-table';

    import * as Select from '$comp/ui/select';
    import { cn } from '$lib/utils';

    interface Props {
        joined?: boolean;
        onPageSizeChange?: () => void;
        table: Table<StockFeatures, TData>;
        value: number;
    }

    let { joined = false, onPageSizeChange, table, value = $bindable() }: Props = $props();

    type Item = { label: string; value: string };
    const items: Item[] = [
        {
            label: '5',
            value: '5'
        },
        {
            label: '10',
            value: '10'
        },
        {
            label: '20',
            value: '20'
        },
        {
            label: '30',
            value: '30'
        },
        {
            label: '40',
            value: '40'
        },
        {
            label: '50',
            value: '50'
        }
    ];

    let valueString = $derived(value + '');
    let selected = $derived((items.find((item) => item.value === valueString) || items[0]) as Item);

    function onValueChange(newValue: string) {
        value = Number(newValue);
        table.setPageSize(Number(newValue));
        onPageSizeChange?.();
    }
</script>

<Select.Root type="single" {items} value={valueString} {onValueChange}>
    <Select.Trigger
        aria-label="Rows per page"
        class={cn('min-w-14 sm:min-w-18', joined && 'rounded-none border-y-0')}
        size={joined ? 'default' : 'sm'}
        title="Rows per page"
    >
        {selected.label}<span class="hidden sm:inline"> rows</span>
    </Select.Trigger>
    <Select.Content>
        <Select.Group>
            {#each items as item (item.value)}
                <Select.Item value={item.value}>{item.label} rows</Select.Item>
            {/each}
        </Select.Group>
    </Select.Content>
</Select.Root>
