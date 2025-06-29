<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';
    import type { Snippet } from 'svelte';

    import DataTableViewOptions from './data-table-view-options.svelte';

    interface Props {
        actions?: Snippet;
        children: Snippet;
        table: Table<TData>;
    }

    let { actions, children, table }: Props = $props();
</script>

<div class="flex items-center justify-between gap-x-2">
    <div class="flex flex-1 flex-wrap items-center gap-x-2 gap-y-2">
        {@render children()}
        <div class="ml-auto flex gap-x-2">
            {#if actions}
                {@render actions()}
            {/if}
            <DataTableViewOptions {table} />
        </div>
    </div>
</div>
