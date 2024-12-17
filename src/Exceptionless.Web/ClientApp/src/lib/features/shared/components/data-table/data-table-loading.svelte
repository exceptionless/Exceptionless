<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Snippet } from 'svelte';

    import { Skeleton } from '$comp/ui/skeleton';
    import * as Table from '$comp/ui/table';
    import { type Column, type Table as SvelteTable } from '@tanstack/svelte-table';

    interface Props {
        children?: Snippet;
        isLoading?: boolean;
        table: SvelteTable<TData>;
    }

    let { children, isLoading = true, table }: Props = $props();

    function shouldRenderCell(column: Column<TData, unknown>): boolean {
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
