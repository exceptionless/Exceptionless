<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import Loading from '$comp/Loading.svelte';
    import * as Table from '$comp/ui/table';
    import { type Cell, FlexRender, type Header, type Table as SvelteTable } from '@tanstack/svelte-table';

    import DataTableColumnHeader from './data-table-column-header.svelte';

    interface Props {
        isLoading: boolean;
        rowclick?: (row: TData) => void;
        table: SvelteTable<TData>;
    }

    let { isLoading, rowclick, table }: Props = $props();

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

        if (rowclick) {
            rowclick(cell.row.original);
        }
    }

    const showLoading = $derived(isLoading && table.getRowModel().rows.length === 0);
</script>

<div class="rounded-md border">
    <Table.Root>
        <Table.Header>
            {#each table.getHeaderGroups() as headerGroup}
                <Table.Row>
                    {#each headerGroup.headers as header (header.id)}
                        <Table.Head class={getHeaderColumnClass(header)}>
                            <DataTableColumnHeader column={header.column}
                                ><FlexRender content={header.column.columnDef.header} context={header.getContext()} /></DataTableColumnHeader
                            >
                        </Table.Head>
                    {/each}
                </Table.Row>
            {/each}
        </Table.Header>
        <Table.Body>
            <Table.Row class="hidden text-center only:table-row">
                <Table.Cell colspan={table.getVisibleLeafColumns().length}>No data was found with the current filter.</Table.Cell>
            </Table.Row>
            {#if showLoading}
                <Table.Row class="text-center">
                    <Table.Cell colspan={table.getVisibleLeafColumns().length}>
                        <div class="flex items-center justify-center">
                            <Loading class="mr-2" /> Loading...
                        </div>
                    </Table.Cell>
                </Table.Row>
            {/if}
            {#each table.getRowModel().rows as row (row.id)}
                <Table.Row>
                    {#each row.getVisibleCells() as cell (cell.id)}
                        <Table.Cell class={getCellClass(cell)} onclick={() => onCellClick(cell)}>
                            <FlexRender content={cell.column.columnDef.cell} context={cell.getContext()} />
                        </Table.Cell>
                    {/each}
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
</div>
