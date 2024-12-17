<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Snippet } from 'svelte';

    import Loading from '$comp/Loading.svelte';
    import * as Table from '$comp/ui/table';
    import { type Table as SvelteTable } from '@tanstack/svelte-table';

    interface Props {
        children?: Snippet;
        isLoading: boolean;
        table: SvelteTable<TData>;
    }

    let { children, isLoading, table }: Props = $props();

    const showLoading = $derived(isLoading && table.getRowModel().rows.length === 0);
</script>

{#if showLoading}
    <Table.Row class="text-center">
        <Table.Cell colspan={table.getVisibleLeafColumns().length}>
            {#if children}
                {@render children()}
            {:else}
                <div class="flex items-center justify-center">
                    <Loading class="mr-2" /> Loading...
                </div>
            {/if}
        </Table.Cell>
    </Table.Row>
{/if}
