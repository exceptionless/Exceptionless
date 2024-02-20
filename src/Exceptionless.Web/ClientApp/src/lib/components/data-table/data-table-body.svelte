<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { type Readable } from 'svelte/store';
    import { flexRender, type Cell, type Header, type Table as SvelteTable } from '@tanstack/svelte-table';

    import DataTableColumnHeader from './data-table-column-header.svelte';
    import * as Table from '$comp/ui/table';

    type TData = $$Generic;
    export let table: Readable<SvelteTable<TData>>;

    const dispatch = createEventDispatcher();

    function getHeaderColumnClass(header: Header<TData, unknown>) {
        const classes = [(header.column.columnDef.meta as { class?: string })?.class || ''];
        return classes.filter(Boolean).join(' ');
    }

    function getCellClass(cell: Cell<TData, unknown>) {
        if (cell.column.id === 'select') {
            return;
        }

        return 'cursor-pointer hover';
    }

    function onCellClick(cell: Cell<TData, unknown>): void {
        if (cell.column.id === 'select') {
            return;
        }

        dispatch('rowclick', cell.row.original);
    }
</script>

<div class="rounded-md border">
    <Table.Root>
        <Table.Header>
            {#each $table.getHeaderGroups() as headerGroup}
                <Table.Row>
                    {#each headerGroup.headers as header (header.id)}
                        <Table.Head class={getHeaderColumnClass(header)}>
                            <DataTableColumnHeader column={header.column}
                                ><svelte:component this={flexRender(header.column.columnDef.header, header.getContext())} /></DataTableColumnHeader
                            >
                        </Table.Head>
                    {/each}
                </Table.Row>
            {/each}
        </Table.Header>
        <Table.Body>
            <Table.Row class="hidden text-center only:table-row">
                <Table.Cell colspan={$table.getVisibleLeafColumns().length}>No data was found with the current filter.</Table.Cell>
            </Table.Row>
            {#each $table.getRowModel().rows as row (row.id)}
                <Table.Row>
                    {#each row.getVisibleCells() as cell (cell.id)}
                        <Table.Cell on:click={() => onCellClick(cell)} class={getCellClass(cell)}>
                            <svelte:component this={flexRender(cell.column.columnDef.cell, cell.getContext())} />
                        </Table.Cell>
                    {/each}
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
</div>
