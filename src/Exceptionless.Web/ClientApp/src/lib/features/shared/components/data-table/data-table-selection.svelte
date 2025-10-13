<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Table } from '@tanstack/svelte-table';

    import Number from '$comp/formatters/number.svelte';

    interface Props {
        table: Table<TData>;
    }

    let { table }: Props = $props();
</script>

<div class="text-muted-foreground flex text-sm text-pretty min-w-0">
    {#if table.getSelectedRowModel().rows.length > 0}
        <span class="truncate"><Number value={Object.keys(table.getSelectedRowModel().rows).length} /> selected</span>
    {/if}
</div>
