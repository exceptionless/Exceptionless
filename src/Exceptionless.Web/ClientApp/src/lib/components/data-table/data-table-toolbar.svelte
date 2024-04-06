<script lang="ts">
    import type { Table } from '@tanstack/svelte-table';
    import { type Readable } from 'svelte/store';

    import DataTableViewOptions from './data-table-view-options.svelte';

    type TData = $$Generic;
    export let table: Readable<Table<TData>>;
</script>

<div class="flex items-center justify-between gap-x-2">
    <div class="flex flex-1 flex-wrap items-center gap-x-2 gap-y-2">
        <slot />
        <div class="ml-auto flex">
            <DataTableViewOptions {table} />
        </div>
    </div>
</div>
