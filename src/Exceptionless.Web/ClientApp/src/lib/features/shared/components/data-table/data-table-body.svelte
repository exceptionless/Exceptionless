<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';

    import { A } from '$comp/typography';
    import * as Table from '$comp/ui/table';
    import { type Cell, FlexRender, type Header, type RowData, type StockFeatures, type Table as SvelteTable } from '@tanstack/svelte-table';

    import DataTableColumnHeader from './data-table-column-header.svelte';

    interface Props {
        children?: Snippet;
        rowClick?: (row: TData, event?: MouseEvent) => void;
        rowHref?: (row: TData) => string;
        table: SvelteTable<StockFeatures, TData>;
    }

    let { children, rowClick, rowHref, table }: Props = $props();

    function getHeaderColumnClass(header: Header<StockFeatures, TData, unknown>) {
        const metaClass = (header.column.columnDef.meta as { class?: string })?.class || '';
        if (!metaClass) {
            return '';
        }

        if (metaClass.includes('text-right')) {
            return [metaClass, 'justify-end'].join(' ');
        }

        return metaClass;
    }

    function getCellClass(cell: Cell<StockFeatures, TData, unknown>) {
        if (cell.column.id === 'select') {
            return;
        }

        const metaClass = (cell.column.columnDef.meta as { class?: string })?.class ?? '';
        const classes = rowClick ? ['cursor-pointer', 'truncate', 'max-w-sm', metaClass] : ['truncate', 'max-w-sm', metaClass];
        return classes.filter(Boolean).join(' ');
    }

    function onCellClick(event: MouseEvent, cell: Cell<StockFeatures, TData, unknown>): void {
        if (cell.column.id === 'select') {
            return;
        }

        if (!rowClick) {
            return;
        }

        // If we have an href and modifier keys are pressed, let the browser handle it
        if (rowHref && (event.ctrlKey || event.metaKey || event.shiftKey)) {
            return;
        }

        // For regular clicks with href, prevent default navigation
        if (rowHref) {
            event.preventDefault();
        }

        // Call the row click handler, passing the event so consumer can override if needed
        rowClick(cell.row.original, event);
    }
</script>

<div class="rounded-md border">
    <Table.Root>
        <Table.Header class="bg-card">
            {#each table.getHeaderGroups() as headerGroup (headerGroup.id)}
                <Table.Row>
                    {#each headerGroup.headers as header (header.id)}
                        {@const headerClass = getHeaderColumnClass(header)}
                        <Table.Head class={headerClass}>
                            <DataTableColumnHeader class={headerClass} column={header.column}><FlexRender {header} /></DataTableColumnHeader>
                        </Table.Head>
                    {/each}
                </Table.Row>
            {/each}
        </Table.Header>
        <Table.Body>
            {#if children}
                {@render children()}
            {/if}
            {#each table.getRowModel().rows as row (row.id)}
                <Table.Row>
                    {#each row.getVisibleCells() as cell (cell.id)}
                        {#if rowHref && cell.row.original}
                            {@const href = rowHref(cell.row.original)}
                            <A {href} class="contents" onclick={(event) => onCellClick(event, cell)} variant="ghost">
                                <Table.Cell class={getCellClass(cell)}>
                                    <FlexRender {cell} />
                                </Table.Cell>
                            </A>
                        {:else}
                            <Table.Cell class={getCellClass(cell)} onclick={(event) => onCellClick(event, cell)}>
                                <FlexRender {cell} />
                            </Table.Cell>
                        {/if}
                    {/each}
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
</div>
