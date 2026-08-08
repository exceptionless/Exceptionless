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

    const selectColumnClass = 'w-8 min-w-8 max-w-8';
    const selectColumnWidth = 32;

    function getHeaderColumnClass(header: Header<StockFeatures, TData, unknown>) {
        if (header.column.id === 'select') {
            return selectColumnClass;
        }

        const metaClass = getMetaClass(header.column.columnDef.meta);
        if (!metaClass) {
            return '';
        }

        const className = getVisibleDataColumnCount() === 1 ? removeWidthClasses(metaClass) : metaClass;
        if (className.includes('text-right')) {
            return [className, 'justify-end'].join(' ');
        }

        if (className.includes('text-center')) {
            return [className, 'justify-center'].join(' ');
        }

        return className;
    }

    function getCellClass(cell: Cell<StockFeatures, TData, unknown>) {
        if (cell.column.id === 'select') {
            return selectColumnClass;
        }

        const isOnlyDataColumn = getVisibleDataColumnCount() === 1;
        const metaClass = isOnlyDataColumn ? removeWidthClasses(getMetaClass(cell.column.columnDef.meta)) : getMetaClass(cell.column.columnDef.meta);
        const classes = rowClick
            ? ['cursor-pointer', 'truncate', !isOnlyDataColumn && 'max-w-sm', metaClass]
            : ['truncate', !isOnlyDataColumn && 'max-w-sm', metaClass];
        return classes.filter(Boolean).join(' ');
    }

    function getHeaderContentClass(header: Header<StockFeatures, TData, unknown>, headerClass: string): string {
        return header.column.getCanResize() ? removeWidthClasses(headerClass) : headerClass;
    }

    function getColumnStyle(column: Cell<StockFeatures, TData, unknown>['column'] | Header<StockFeatures, TData, unknown>['column']): string | undefined {
        if (column.id === 'select') {
            return `width: ${selectColumnWidth}px; min-width: ${selectColumnWidth}px; max-width: ${selectColumnWidth}px;`;
        }

        if (!column.getCanResize() || getVisibleDataColumnCount() === 1) {
            return undefined;
        }

        return `width: ${column.getSize()}px; min-width: ${column.getSize()}px; max-width: ${column.getSize()}px;`;
    }

    function getMetaClass(meta: unknown): string {
        return (meta as { class?: string })?.class ?? '';
    }

    function getVisibleDataColumnCount(): number {
        return table.getVisibleLeafColumns().filter((column) => column.id !== 'select').length;
    }

    function hasSelectColumn(): boolean {
        return table.getVisibleLeafColumns().some((column) => column.id === 'select');
    }

    function isWidthClass(className: string): boolean {
        return /^(?:max-w|min-w|w)-/.test(className);
    }

    function onCellClick(event: MouseEvent, cell: Cell<StockFeatures, TData, unknown>): void {
        if (cell.column.id === 'select') {
            return;
        }

        const target = event.target as HTMLElement | null;
        const interactiveTarget = target?.closest('button, input, select, textarea, [role="button"], [role="menuitem"], [data-row-click-ignore]');
        if (interactiveTarget) {
            event.preventDefault();
            event.stopPropagation();
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

    function onResizeKeydown(event: KeyboardEvent, header: Header<StockFeatures, TData, unknown>): void {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        const delta = event.key === 'ArrowLeft' ? -16 : 16;
        table.setColumnSizing((current) => ({
            ...current,
            [header.column.id]: Math.min(
                header.column.columnDef.maxSize ?? Number.MAX_SAFE_INTEGER,
                Math.max(header.column.columnDef.minSize ?? 20, header.column.getSize() + delta)
            )
        }));
    }

    function removeWidthClasses(className: string): string {
        return className
            .split(' ')
            .filter((part) => !isWidthClass(part))
            .join(' ');
    }
</script>

<div class="rounded-md border">
    <Table.Root class={hasSelectColumn() ? 'table-fixed' : undefined}>
        <Table.Header class="bg-card">
            {#each table.getHeaderGroups() as headerGroup (headerGroup.id)}
                <Table.Row>
                    {#each headerGroup.headers as header (header.id)}
                        {@const headerClass = getHeaderColumnClass(header)}
                        <Table.Head class={[headerClass, header.column.getCanResize() && 'group relative']} style={getColumnStyle(header.column)}>
                            <DataTableColumnHeader class={getHeaderContentClass(header, headerClass)} column={header.column}
                                ><FlexRender {header} /></DataTableColumnHeader
                            >
                            {#if header.column.getCanResize()}
                                <button
                                    aria-label={`Resize ${header.column.id} column`}
                                    class={[
                                        'hover:bg-primary focus-visible:bg-primary absolute top-0 right-0 z-10 h-full w-1.5 cursor-col-resize touch-none outline-none select-none',
                                        'after:bg-border after:absolute after:top-1/4 after:right-0 after:h-1/2 after:w-px',
                                        header.column.getIsResizing() && 'bg-primary'
                                    ]}
                                    ondblclick={() => header.column.resetSize()}
                                    onkeydown={(event) => onResizeKeydown(event, header)}
                                    onmousedown={header.getResizeHandler()}
                                    ontouchstart={header.getResizeHandler()}
                                    title={`Resize ${header.column.id} column`}
                                    type="button"
                                ></button>
                            {/if}
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
                <Table.Row
                    tabindex={rowClick ? 0 : undefined}
                    onkeydown={rowClick
                        ? (event) => {
                              if (event.key === 'Enter' || event.key === ' ') {
                                  event.preventDefault();
                                  const firstCell = row.getVisibleCells()[0];
                                  if (firstCell) {
                                      rowClick(firstCell.row.original);
                                  }
                              }
                          }
                        : undefined}
                >
                    {#each row.getVisibleCells() as cell (cell.id)}
                        {#if rowHref && cell.row.original}
                            {@const href = rowHref(cell.row.original)}
                            <A {href} class="contents" onclick={(event) => onCellClick(event, cell)} variant="ghost">
                                <Table.Cell class={getCellClass(cell)} style={getColumnStyle(cell.column)}>
                                    <FlexRender {cell} />
                                </Table.Cell>
                            </A>
                        {:else}
                            <Table.Cell class={getCellClass(cell)} onclick={(event) => onCellClick(event, cell)} style={getColumnStyle(cell.column)}>
                                <FlexRender {cell} />
                            </Table.Cell>
                        {/if}
                    {/each}
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
</div>
