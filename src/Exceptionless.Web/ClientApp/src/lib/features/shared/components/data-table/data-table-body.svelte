<script module lang="ts">
    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { Snippet } from 'svelte';

    import { A } from '$comp/typography';
    import * as Table from '$comp/ui/table';
    import { type Cell, FlexRender, type Header, type RowData, type StockFeatures, type Table as SvelteTable } from '@tanstack/svelte-table';

    import { getDataTableColumnMeta, supportsColumnWrapping } from './column-meta';
    import DataTableColumnHeader from './data-table-column-header.svelte';

    interface Props {
        autoFillColumnId?: null | string;
        children?: Snippet;
        onAutoFillColumnResized?: (columnId: string) => void;
        rowClick?: (row: TData, event?: MouseEvent) => void;
        rowHref?: (row: TData) => string;
        table: SvelteTable<StockFeatures, TData>;
        wrappedColumnIds?: readonly string[];
    }

    let { autoFillColumnId, children, onAutoFillColumnResized, rowClick, rowHref, table, wrappedColumnIds = [] }: Props = $props();

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
        const contentClass = isColumnWrapped(cell.column)
            ? 'group/wrapped whitespace-normal break-words [&_.line-clamp-1]:line-clamp-none [&_.line-clamp-2]:line-clamp-none'
            : 'truncate';
        const classes = rowClick
            ? ['cursor-pointer', contentClass, !isOnlyDataColumn && 'max-w-sm', metaClass]
            : [contentClass, !isOnlyDataColumn && 'max-w-sm', metaClass];
        return classes.filter(Boolean).join(' ');
    }

    function isColumnWrapped(column: Cell<StockFeatures, TData, unknown>['column']): boolean {
        return supportsColumnWrapping(column.columnDef.meta) && wrappedColumnIds.includes(column.id);
    }

    function getHeaderContentClass(header: Header<StockFeatures, TData, unknown>, headerClass: string): string {
        return header.column.getCanResize() ? removeWidthClasses(headerClass) : headerClass;
    }

    function getColumnStyle(column: Cell<StockFeatures, TData, unknown>['column'] | Header<StockFeatures, TData, unknown>['column']): string | undefined {
        if (column.id === 'select') {
            return `width: ${selectColumnWidth}px; min-width: ${selectColumnWidth}px; max-width: ${selectColumnWidth}px;`;
        }

        if (hasSelectColumn() && column.id === getFlexibleDataColumnId()) {
            return 'width: 100%;';
        }

        if (!column.getCanResize() || getVisibleDataColumnCount() === 1) {
            return undefined;
        }

        return `width: ${column.getSize()}px; min-width: ${column.getSize()}px; max-width: ${column.getSize()}px;`;
    }

    function getMetaClass(meta: unknown): string {
        return getDataTableColumnMeta(meta).class ?? '';
    }

    function getFlexibleDataColumnId(): string | undefined {
        const columnSizing = table.atoms.columnSizing?.get() ?? {};
        const visibleDataColumns = getVisibleDataColumns();
        if (autoFillColumnId !== undefined) {
            if (autoFillColumnId === null) {
                return undefined;
            }

            const autoFillColumn = visibleDataColumns.find((column) => column.id === autoFillColumnId);
            return autoFillColumn && columnSizing[autoFillColumn.id] === undefined ? autoFillColumn.id : undefined;
        }

        const fullWidthColumns = visibleDataColumns.filter((column) => getMetaClass(column.columnDef.meta).split(' ').includes('w-full'));
        if (fullWidthColumns.length > 0) {
            return fullWidthColumns.find((column) => columnSizing[column.id] === undefined)?.id;
        }

        return visibleDataColumns.filter((column) => columnSizing[column.id] === undefined).at(-1)?.id;
    }

    function getVisibleDataColumnCount(): number {
        return getVisibleDataColumns().length;
    }

    function getVisibleDataColumns() {
        return table.getVisibleLeafColumns().filter((column) => column.id !== 'select');
    }

    function getTableStyle(): string | undefined {
        if (!hasSelectColumn()) {
            return undefined;
        }

        const minimumWidth = selectColumnWidth + getVisibleDataColumns().reduce((total, column) => total + column.getSize(), 0);
        return getFlexibleDataColumnId() ? `min-width: ${minimumWidth}px;` : `width: ${minimumWidth}px; min-width: ${minimumWidth}px;`;
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
        const currentSize = getResizeStartSize(event, header);
        handleAutoFillColumnResize(header);
        table.setColumnSizing((current) => ({
            ...current,
            [header.column.id]: Math.min(
                header.column.columnDef.maxSize ?? Number.MAX_SAFE_INTEGER,
                Math.max(header.column.columnDef.minSize ?? 20, currentSize + delta)
            )
        }));
    }

    function onResizeStart(event: MouseEvent | TouchEvent, header: Header<StockFeatures, TData, unknown>): void {
        const currentSize = getResizeStartSize(event, header);
        if (currentSize === header.column.getSize()) {
            header.getResizeHandler()(event);
            return;
        }

        const startPosition = getClientPosition(event);
        const document = (event.currentTarget as HTMLElement | null)?.ownerDocument;
        if (startPosition === undefined || !document) {
            header.getResizeHandler()(event);
            return;
        }

        const startEvent = event;
        const removePendingListeners = () => {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseEnd);
            document.removeEventListener('touchmove', onTouchMove);
            document.removeEventListener('touchend', onTouchEnd);
            document.removeEventListener('touchcancel', onTouchEnd);
        };

        const startResize = (position: number) => {
            if (position === startPosition) {
                return;
            }

            removePendingListeners();
            handleAutoFillColumnResize(header);
            table.setColumnSizing((current) => ({
                ...current,
                [header.column.id]: currentSize
            }));
            header.getResizeHandler()(startEvent);
            setColumnSize(header, currentSize + position - startPosition);
        };

        const onMouseMove = (moveEvent: MouseEvent) => startResize(moveEvent.clientX);
        const onMouseEnd = () => removePendingListeners();
        const onTouchMove = (moveEvent: TouchEvent) => {
            const position = getClientPosition(moveEvent);
            if (position !== undefined) {
                startResize(position);
            }
        };

        const onTouchEnd = () => removePendingListeners();

        if (event instanceof TouchEvent) {
            document.addEventListener('touchmove', onTouchMove);
            document.addEventListener('touchend', onTouchEnd);
            document.addEventListener('touchcancel', onTouchEnd);
        } else {
            document.addEventListener('mousemove', onMouseMove);
            document.addEventListener('mouseup', onMouseEnd);
        }
    }

    function handleAutoFillColumnResize(header: Header<StockFeatures, TData, unknown>): void {
        if (header.column.id === autoFillColumnId) {
            onAutoFillColumnResized?.(header.column.id);
        }
    }

    function getClientPosition(event: MouseEvent | TouchEvent): number | undefined {
        return event instanceof TouchEvent ? event.touches[0]?.clientX : event.clientX;
    }

    function setColumnSize(header: Header<StockFeatures, TData, unknown>, size: number): void {
        table.setColumnSizing((current) => ({
            ...current,
            [header.column.id]: Math.min(header.column.columnDef.maxSize ?? Number.MAX_SAFE_INTEGER, Math.max(header.column.columnDef.minSize ?? 20, size))
        }));
    }

    function getResizeStartSize(event: KeyboardEvent | MouseEvent | TouchEvent, header: Header<StockFeatures, TData, unknown>): number {
        if (header.column.id !== getFlexibleDataColumnId()) {
            return header.column.getSize();
        }

        const headerElement = (event.currentTarget as HTMLElement | null)?.closest('th');
        return headerElement?.getBoundingClientRect().width || header.column.getSize();
    }

    function removeWidthClasses(className: string): string {
        return className
            .split(' ')
            .filter((part) => !isWidthClass(part))
            .join(' ');
    }
</script>

<div class="rounded-md border">
    <Table.Root class={hasSelectColumn() ? 'table-fixed' : undefined} style={getTableStyle()}>
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
                                    onmousedown={(event) => onResizeStart(event, header)}
                                    ontouchstart={(event) => onResizeStart(event, header)}
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
                                <Table.Cell
                                    class={getCellClass(cell)}
                                    data-wrap={isColumnWrapped(cell.column) ? 'true' : undefined}
                                    style={getColumnStyle(cell.column)}
                                >
                                    <FlexRender {cell} />
                                </Table.Cell>
                            </A>
                        {:else}
                            <Table.Cell
                                class={getCellClass(cell)}
                                data-wrap={isColumnWrapped(cell.column) ? 'true' : undefined}
                                onclick={(event) => onCellClick(event, cell)}
                                style={getColumnStyle(cell.column)}
                            >
                                <FlexRender {cell} />
                            </Table.Cell>
                        {/if}
                    {/each}
                </Table.Row>
            {/each}
        </Table.Body>
    </Table.Root>
</div>
