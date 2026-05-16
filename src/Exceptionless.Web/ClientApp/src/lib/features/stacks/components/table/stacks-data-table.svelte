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
    <DataTable.Body {rowClick} {rowHref} {table}>
        {#if isLoading}
            <DelayedRender>
                <DataTable.Loading {table} />
            </DelayedRender>
        {:else}
            <DataTable.Empty {table} />
        {/if}
    </DataTable.Body>
    <DataTable.Footer {table} class="w-full">
        {#if footerChildren}
            {@render footerChildren()}
        {:else}
            <div class="grid w-full grid-cols-1 items-center gap-2 sm:grid-cols-3">
                <div class="flex min-w-0 items-center gap-2">
                    <DataTable.Selection {table} />
                </div>

                <div class="flex min-w-0 items-center justify-center">
                    <DataTable.PageCount {table} />
                </div>

                <div class="flex min-w-0 items-center justify-end gap-4">
                    <DataTable.PageSize bind:value={limit} {table} />
                    <DataTable.Pagination {table} />
                </div>
            </div>
        {/if}
    </DataTable.Footer>
</DataTable.Root>
