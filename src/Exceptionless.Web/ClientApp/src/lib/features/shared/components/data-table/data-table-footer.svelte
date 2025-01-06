<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Snippet } from 'svelte';
    import type { HTMLAttributes } from 'svelte/elements';

    import { type Table as SvelteTable } from '@tanstack/svelte-table';

    import DataTablePageCount from './data-table-page-count.svelte';
    import DataTablePagination from './data-table-pagination.svelte';
    import DataTableSelection from './data-table-selection.svelte';

    type Props = HTMLAttributes<Element> & {
        children?: Snippet;
        table: SvelteTable<TData>;
    };

    let { children, class: className, table }: Props = $props();
</script>

<div class={['flex items-center justify-between', className]}>
    {#if children}
        {@render children()}
    {:else}
        <DataTableSelection {table} />
        <div class="flex items-center space-x-6 lg:space-x-8">
            <DataTablePageCount {table} />
            <DataTablePagination {table} />
        </div>
    {/if}
</div>
