<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Snippet } from 'svelte';

    import { Button } from '$comp/ui/button';
    import * as Table from '$comp/ui/table';
    import { type Table as SvelteTable } from '@tanstack/svelte-table';

    interface Props {
        children?: Snippet;
        refresh: () => Promise<void>;
        table: SvelteTable<TData>;
    }

    let { children, refresh, table }: Props = $props();
</script>

<Table.Row class="text-center">
    <Table.Cell colspan={table.getVisibleLeafColumns().length}>
        {#if children}
            {@render children()}
        {:else}
            New data is available!
            <Button variant="link" onclick={refresh} class="px-0">Click here to see the latest changes and reset any selections.</Button>
        {/if}
    </Table.Cell>
</Table.Row>
