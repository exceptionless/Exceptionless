<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';

    import { type Column, type RowData, type StockFeatures, type Table as SvelteTable } from '@tanstack/svelte-table';

    import { Skeleton } from '#comp/ui/skeleton/index.ts';
    import * as Table from '#comp/ui/table/index.ts';

    interface Props {
        children?: Snippet;
        isLoading?: boolean;
        table: SvelteTable<StockFeatures, TData>;
    }

    let { children, isLoading = true, table }: Props = $props();

    function shouldRenderCell(column: Column<StockFeatures, TData, unknown>): boolean {
        return column.id !== 'select';
    }

    const showLoading = $derived(isLoading && table.getRowModel().rows.length === 0);
</script>

{#if showLoading}
    <Table.Row>
        {#if children}
            {@render children()}
        {:else}
            {#each table.getVisibleLeafColumns() as cell (cell.id)}
                <Table.Cell>
                    {#if shouldRenderCell(cell)}
                        <Skeleton class="h-[20px] w-full rounded-full" />
                    {/if}
                </Table.Cell>
            {/each}
        {/if}
    </Table.Row>
{/if}
