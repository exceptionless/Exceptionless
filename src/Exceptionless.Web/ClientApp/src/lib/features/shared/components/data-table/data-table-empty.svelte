<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Snippet } from 'svelte';

    import * as Table from '$comp/ui/table';
    import { type Table as SvelteTable } from '@tanstack/svelte-table';

    interface Props {
        children?: Snippet;
        table: SvelteTable<TData>;
    }

    let { children, table }: Props = $props();
</script>

<Table.Row class="hidden text-center only:table-row">
    <Table.Cell colspan={table.getVisibleLeafColumns().length}>
        {#if children}
            {@render children()}
        {:else}
            No data was found with the current filter.
        {/if}
    </Table.Cell>
</Table.Row>
