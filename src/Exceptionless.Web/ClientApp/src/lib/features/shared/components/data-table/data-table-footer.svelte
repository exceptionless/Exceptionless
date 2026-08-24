<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    import { type RowData, type StockFeatures, type Table } from '@tanstack/svelte-table';

    import DataTablePageCount from './data-table-page-count.svelte';
    import DataTablePagination from './data-table-pagination.svelte';
    import DataTableSelection from './data-table-selection.svelte';

    type Props = HTMLAttributes<Element> & {
        children?: Snippet;
        table: Table<StockFeatures, TData>;
    };

    let { children, class: className, table }: Props = $props();
</script>

<div
    aria-label="Table controls"
    class={[
        'border-border bg-background/95 sticky top-2 z-30 flex w-full flex-nowrap items-center justify-between gap-0 rounded-lg border backdrop-blur-sm',
        className
    ]}
    data-slot="data-table-footer"
    role="toolbar"
>
    {#if children}
        {@render children()}
    {:else}
        <DataTableSelection {table} />
        <div class="flex items-center gap-4">
            <DataTablePageCount {table} />
            <DataTablePagination {table} />
        </div>
    {/if}
</div>
