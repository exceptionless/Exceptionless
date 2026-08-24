<script lang="ts">
    import type { Stack } from '$features/stacks/models';
    import type { Snippet } from 'svelte';

    import * as DataTable from '$comp/data-table';
    import DelayedRender from '$comp/delayed-render.svelte';
    import { type StockFeatures, type Table } from '@tanstack/svelte-table';

    interface Props {
        footerChildren?: Snippet;
        isLoading: boolean;
        limit: number;
        rowClick?: (row: Stack) => void;
        rowHref?: (row: Stack) => string;
        table: Table<StockFeatures, Stack>;
    }

    let { footerChildren, isLoading, limit = $bindable(), rowClick, rowHref, table }: Props = $props();
</script>

<DataTable.Root>
    <DataTable.Footer {table} class="w-full">
        {#if footerChildren}
            {@render footerChildren()}
        {:else}
            <DataTable.Selection {table} />
            <DataTable.Pager bind:value={limit} {table} />
        {/if}
    </DataTable.Footer>
    <DataTable.Body {rowClick} {rowHref} {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
    </DataTable.Body>
</DataTable.Root>
