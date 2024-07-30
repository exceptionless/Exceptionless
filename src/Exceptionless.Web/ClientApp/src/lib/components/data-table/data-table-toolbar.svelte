<script lang="ts" context="module">
    type TData = unknown;
</script>

<script lang="ts" generics="TData">
    import type { Snippet } from 'svelte';
    import type { Table } from '@tanstack/svelte-table';

    import DataTableViewOptions from './data-table-view-options.svelte';

    interface Props {
        children: Snippet;
        table: Table<TData>;
    }

    let { children, table }: Props = $props();
</script>

<div class="flex items-center justify-between gap-x-2">
    <div class="flex flex-1 flex-wrap items-center gap-x-2 gap-y-2">
        {@render children()}
        <div class="ml-auto flex">
            <DataTableViewOptions {table} />
        </div>
    </div>
</div>
