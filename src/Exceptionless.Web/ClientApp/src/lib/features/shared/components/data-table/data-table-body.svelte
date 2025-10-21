<script module lang="ts">
    type TData = unknown;
</script>

<script generics="TData" lang="ts">
    import type { Snippet } from 'svelte';

    import { A } from '$comp/typography';
    import * as Table from '$comp/ui/table';
    import { type Cell, FlexRender, type Header, type Table as SvelteTable } from '@tanstack/svelte-table';

    import DataTableColumnHeader from './data-table-column-header.svelte';

    interface Props {
        children?: Snippet;
        rowClick?: (row: TData, event?: MouseEvent) => void;
        rowHref?: (row: TData) => string;
        table: SvelteTable<TData>;
    }

    let { children, rowClick, rowHref, table }: Props = $props();

    function getHeaderColumnClass(header: Header<TData, unknown>) {
        const classes = [(header.column.columnDef.meta as { class?: string })?.class || ''];
        return classes.filter(Boolean).join(' ');
    }

    function getCellClass(cell: Cell<TData, unknown>) {
        if (cell.column.id === 'select') {
            return;
        }

        return 'cursor-pointer hover truncate max-w-sm';
    }

    function onCellClick(event: MouseEvent, cell: Cell<TData, unknown>): void {
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
                                    <FlexRender content={cell.column.columnDef.cell} context={cell.getContext()} />
                                </Table.Cell>
                            </A>
                        {:else}
                            <Table.Cell class={getCellClass(cell)} onclick={(event) => onCellClick(event, cell)}>
                                <FlexRender content={cell.column.columnDef.cell} context={cell.getContext()} />
                            </Table.Cell>
                        {/if}
                    {/each}
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
</div>
