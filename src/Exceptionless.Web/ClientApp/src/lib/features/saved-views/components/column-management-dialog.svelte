<script module lang="ts">
    import type { RowData } from '@tanstack/svelte-table';

    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { StockFeatures, Table } from '@tanstack/svelte-table';

    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import ChevronUp from '@lucide/svelte/icons/chevron-up';
    import GripVertical from '@lucide/svelte/icons/grip-vertical';
    import Plus from '@lucide/svelte/icons/plus';
    import X from '@lucide/svelte/icons/x';

    interface Props {
        open: boolean;
        table: Table<StockFeatures, TData>;
    }

    let { open = $bindable(), table }: Props = $props();

    let draggedColumnId = $state<null | string>(null);

    const allColumns = $derived(table.getAllLeafColumns().filter((column) => column.id !== 'select'));
    const visibleColumns = $derived(allColumns.filter((column) => column.getIsVisible()));
    const availableColumns = $derived(allColumns.filter((column) => column.getCanHide() && !column.getIsVisible()));

    function getColumnLabel(column: (typeof allColumns)[number]): string {
        if (typeof column.columnDef.header === 'string') {
            return column.columnDef.header;
        }

        return column.id.replace(/[_-]/g, ' ').replace(/\w\S*/g, (word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase());
    }

    function addColumn(column: (typeof allColumns)[number]): void {
        column.toggleVisibility(true);
    }

    function removeColumn(column: (typeof allColumns)[number]): void {
        if (visibleColumns.length > 1) {
            column.toggleVisibility(false);
        }
    }

    function canRemoveColumn(column: (typeof allColumns)[number]): boolean {
        return column.getCanHide() && visibleColumns.length > 1;
    }

    function moveColumnUp(columnId: string): void {
        const columnIds = visibleColumns.map((c) => c.id);
        const index = columnIds.indexOf(columnId);
        if (index <= 0) {return;}

        const temp = columnIds[index]!;
        columnIds[index] = columnIds[index - 1]!;
        columnIds[index - 1] = temp;
        applyColumnOrder(columnIds);
    }

    function moveColumnDown(columnId: string): void {
        const columnIds = visibleColumns.map((c) => c.id);
        const index = columnIds.indexOf(columnId);
        if (index === -1 || index >= columnIds.length - 1) {return;}

        const temp = columnIds[index]!;
        columnIds[index] = columnIds[index + 1]!;
        columnIds[index + 1] = temp;
        applyColumnOrder(columnIds);
    }

    function applyColumnOrder(columnIds: string[]): void {
        const hiddenIds = allColumns.filter((c) => !c.getIsVisible()).map((c) => c.id);
        table.setColumnOrder(['select', ...columnIds, ...hiddenIds]);
    }

    function handleDragStart(event: DragEvent, columnId: string): void {
        draggedColumnId = columnId;
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', columnId);
        }
    }

    function handleDragOver(event: DragEvent, targetColumnId: string): void {
        event.preventDefault();
        if (event.dataTransfer) {
            event.dataTransfer.dropEffect = 'move';
        }

        if (draggedColumnId && draggedColumnId !== targetColumnId) {
            const columnIds = visibleColumns.map((c) => c.id);
            const fromIndex = columnIds.indexOf(draggedColumnId);
            const toIndex = columnIds.indexOf(targetColumnId);
            if (fromIndex === -1 || toIndex === -1) {return;}

            const [moved] = columnIds.splice(fromIndex, 1);
            if (moved) {
                columnIds.splice(toIndex, 0, moved);
                applyColumnOrder(columnIds);
            }
        }
    }

    function handleDragEnd(): void {
        draggedColumnId = null;
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="sm:max-w-2xl" preventScroll={false} overlayClass="bg-black/20 supports-backdrop-filter:backdrop-blur-none">
        <Dialog.Header>
            <Dialog.Title>Manage Columns</Dialog.Title>
            <Dialog.Description>
                <Muted class="text-xs">Add columns from the available list and reorder selected columns.</Muted>
            </Dialog.Description>
        </Dialog.Header>

        <div class="grid grid-cols-2 gap-4">
            <!-- Available columns -->
            <div class="flex flex-col gap-2">
                <h3 class="text-sm font-medium">Available</h3>
                <div class="border-input rounded-md border">
                    <div class="max-h-64 overflow-y-auto p-2">
                        {#if availableColumns.length === 0}
                            <p class="text-muted-foreground py-4 text-center text-sm">All columns are visible</p>
                        {:else}
                            {#each availableColumns as column (column.id)}
                                <div class="hover:bg-accent flex items-center justify-between rounded-sm px-2 py-1.5 text-sm">
                                    <span>{getColumnLabel(column)}</span>
                                    <Button variant="ghost" size="icon-xs" onclick={() => addColumn(column)} title="Add column">
                                        <Plus class="size-3.5" />
                                    </Button>
                                </div>
                            {/each}
                        {/if}
                    </div>
                </div>
            </div>

            <!-- Selected columns -->
            <div class="flex flex-col gap-2">
                <h3 class="text-sm font-medium">Selected</h3>
                <div class="border-input rounded-md border">
                    <div class="max-h-64 overflow-y-auto p-2" role="list">
                        {#each visibleColumns as column, index (column.id)}
                            <div
                                class={[
                                    'group/column flex cursor-grab items-center gap-1 rounded-sm px-2 py-1.5 text-sm active:cursor-grabbing',
                                    draggedColumnId === column.id && 'bg-accent/70'
                                ]}
                                draggable="true"
                                ondragstart={(event) => handleDragStart(event, column.id)}
                                ondragover={(event) => handleDragOver(event, column.id)}
                                ondragend={handleDragEnd}
                                role="listitem"
                            >
                                <GripVertical class="text-muted-foreground/60 size-3.5 shrink-0 opacity-0 transition-opacity group-hover/column:opacity-100" />
                                <span class="min-w-0 flex-1 truncate">{getColumnLabel(column)}</span>
                                <div class="flex shrink-0 items-center gap-0.5 opacity-0 transition-opacity group-hover/column:opacity-100">
                                    <Button variant="ghost" size="icon-xs" onclick={() => moveColumnUp(column.id)} disabled={index === 0} title="Move up">
                                        <ChevronUp class="size-3.5" />
                                    </Button>
                                    <Button
                                        variant="ghost"
                                        size="icon-xs"
                                        onclick={() => moveColumnDown(column.id)}
                                        disabled={index === visibleColumns.length - 1}
                                        title="Move down"
                                    >
                                        <ChevronDown class="size-3.5" />
                                    </Button>
                                    {#if canRemoveColumn(column)}
                                        <Button variant="ghost" size="icon-xs" onclick={() => removeColumn(column)} title="Remove column">
                                            <X class="size-3.5" />
                                        </Button>
                                    {/if}
                                </div>
                            </div>
                        {/each}
                    </div>
                </div>
            </div>
        </div>
    </Dialog.Content>
</Dialog.Root>
