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
        variant?: 'floating' | 'simple';
    };

    let { children, class: className, table, variant = 'simple' }: Props = $props();
</script>

<div
    aria-label="Table controls"
    class={[
        'flex w-full items-center',
        variant === 'floating'
            ? 'border-border bg-background/95 sticky top-2 z-30 flex-wrap justify-between gap-0 rounded-lg border backdrop-blur-sm'
            : 'justify-end gap-2',
        className
    ]}
    data-slot="data-table-footer"
    data-variant={variant}
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
