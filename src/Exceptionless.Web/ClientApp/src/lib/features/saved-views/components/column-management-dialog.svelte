<script module lang="ts">
    import type { RowData } from '@tanstack/svelte-table';

    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { StockFeatures, Table } from '@tanstack/svelte-table';

    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import { Checkbox } from '$comp/ui/checkbox';
    import * as Dialog from '$comp/ui/dialog';
    import * as InputGroup from '$comp/ui/input-group';
    import { Separator } from '$comp/ui/separator';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import ChevronUp from '@lucide/svelte/icons/chevron-up';
    import GripVertical from '@lucide/svelte/icons/grip-vertical';
    import Plus from '@lucide/svelte/icons/plus';
    import RotateCcw from '@lucide/svelte/icons/rotate-ccw';
    import Search from '@lucide/svelte/icons/search';
    import X from '@lucide/svelte/icons/x';

    interface Props {
        open: boolean;
        table: Table<StockFeatures, TData>;
    }

    let { open = $bindable(), table }: Props = $props();

    let draggedColumnId = $state<null | string>(null);
    let search = $state('');

    const allColumns = $derived(table.getAllLeafColumns().filter((column) => column.id !== 'select'));
    const visibleColumns = $derived(allColumns.filter((column) => column.getIsVisible()));
    const availableColumns = $derived(allColumns.filter((column) => column.getCanHide() && !column.getIsVisible()));
    const normalizedSearch = $derived(search.trim().toLowerCase());
    const filteredAvailableColumns = $derived(
        normalizedSearch.length === 0 ? availableColumns : availableColumns.filter((column) => getColumnLabel(column).toLowerCase().includes(normalizedSearch))
    );

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
        if (index <= 0) {
            return;
        }

        const temp = columnIds[index]!;
        columnIds[index] = columnIds[index - 1]!;
        columnIds[index - 1] = temp;
        applyColumnOrder(columnIds);
    }

    function moveColumnDown(columnId: string): void {
        const columnIds = visibleColumns.map((c) => c.id);
        const index = columnIds.indexOf(columnId);
        if (index === -1 || index >= columnIds.length - 1) {
            return;
        }

        const temp = columnIds[index]!;
        columnIds[index] = columnIds[index + 1]!;
        columnIds[index + 1] = temp;
        applyColumnOrder(columnIds);
    }

    function applyColumnOrder(columnIds: string[]): void {
        const hiddenIds = allColumns.filter((c) => !c.getIsVisible()).map((c) => c.id);
        table.setColumnOrder(['select', ...columnIds, ...hiddenIds]);
    }

    function resetColumns(): void {
        table.resetColumnVisibility();
        table.resetColumnOrder();
        search = '';
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
            if (fromIndex === -1 || toIndex === -1) {
                return;
            }

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
    <Dialog.Content
        class="max-h-[calc(100dvh-2rem)] gap-0 overflow-hidden p-0 sm:max-w-5xl"
        preventScroll={false}
        overlayClass="bg-black/35 supports-backdrop-filter:backdrop-blur-sm"
    >
        <Dialog.Header class="border-b px-6 py-5 pr-14">
            <Dialog.Title>Column Picker</Dialog.Title>
            <Dialog.Description>Select and reorder the columns displayed in this table.</Dialog.Description>
        </Dialog.Header>

        <div class="grid min-h-0 gap-0 lg:grid-cols-[minmax(0,1fr)_5rem_minmax(0,1fr)]">
            <section class="flex min-h-0 flex-col gap-4 px-6 py-5" aria-labelledby="available-columns-heading">
                <InputGroup.Root class="h-10">
                    <InputGroup.Addon>
                        <Search aria-hidden="true" />
                    </InputGroup.Addon>
                    <InputGroup.Input aria-label="Search available columns" bind:value={search} class="text-sm" placeholder="Search columns..." type="search" />
                </InputGroup.Root>

                <div class="flex items-start justify-between gap-3">
                    <div class="flex flex-col gap-1">
                        <div class="flex items-center gap-2">
                            <h3 id="available-columns-heading" class="text-sm font-semibold">Available Columns</h3>
                            <Badge variant="secondary">{availableColumns.length}</Badge>
                        </div>
                        <p class="text-muted-foreground text-sm">Add optional columns to the table.</p>
                    </div>
                </div>

                <div class="border-input bg-muted/20 rounded-lg border p-2">
                    <div class="flex max-h-[19rem] flex-col gap-1.5 overflow-y-auto pr-1" role="list" aria-label="Available columns">
                        {#if availableColumns.length === 0}
                            <p class="text-muted-foreground py-12 text-center text-sm">All columns are visible</p>
                        {:else if filteredAvailableColumns.length === 0}
                            <p class="text-muted-foreground py-12 text-center text-sm">No columns match your search</p>
                        {:else}
                            {#each filteredAvailableColumns as column (column.id)}
                                <div
                                    class="bg-background hover:bg-muted/70 flex min-h-11 items-center gap-3 rounded-lg border px-3 text-sm shadow-xs transition-colors"
                                    role="listitem"
                                >
                                    <Checkbox aria-label={`Add ${getColumnLabel(column)} column`} checked={false} onclick={() => addColumn(column)} />
                                    <span class="min-w-0 flex-1 truncate font-medium">{getColumnLabel(column)}</span>
                                    <Button variant="ghost" size="icon-sm" onclick={() => addColumn(column)} title={`Add ${getColumnLabel(column)} column`}>
                                        <Plus />
                                        <span class="sr-only">Add {getColumnLabel(column)} column</span>
                                    </Button>
                                </div>
                            {/each}
                        {/if}
                    </div>
                </div>
            </section>

            <div class="bg-muted/30 hidden items-center justify-center border-x lg:flex" aria-hidden="true">
                <div class="flex flex-col gap-3">
                    <div class="border-input bg-background text-muted-foreground flex size-9 items-center justify-center rounded-lg border">
                        <Plus class="size-4" />
                    </div>
                    <div class="border-input bg-background text-muted-foreground flex size-9 items-center justify-center rounded-lg border">
                        <X class="size-4" />
                    </div>
                </div>
            </div>

            <Separator class="lg:hidden" />

            <section class="flex min-h-0 flex-col gap-4 px-6 py-5" aria-labelledby="selected-columns-heading">
                <div class="flex items-start justify-between gap-3">
                    <div class="flex flex-col gap-1">
                        <div class="flex items-center gap-2">
                            <h3 id="selected-columns-heading" class="text-sm font-semibold">Selected Columns</h3>
                            <Badge variant="secondary">{visibleColumns.length}</Badge>
                        </div>
                        <p class="text-muted-foreground text-sm">Drag to reorder. The first item appears on the far left.</p>
                    </div>
                </div>

                <div class="border-input bg-muted/20 rounded-lg border p-2">
                    <div class="flex max-h-[22.75rem] flex-col gap-1.5 overflow-y-auto pr-1" role="list" aria-label="Selected columns">
                        {#each visibleColumns as column, index (column.id)}
                            <div
                                class={[
                                    'bg-background flex min-h-11 cursor-grab items-center gap-3 rounded-lg border px-3 text-sm shadow-xs transition-colors active:cursor-grabbing',
                                    draggedColumnId === column.id && 'bg-muted ring-ring/40 ring-2'
                                ]}
                                draggable="true"
                                ondragstart={(event) => handleDragStart(event, column.id)}
                                ondragover={(event) => handleDragOver(event, column.id)}
                                ondragend={handleDragEnd}
                                role="listitem"
                            >
                                <Checkbox
                                    aria-label={`Remove ${getColumnLabel(column)} column`}
                                    checked={true}
                                    disabled={!canRemoveColumn(column)}
                                    onclick={() => removeColumn(column)}
                                />
                                <span class="min-w-0 flex-1 truncate font-medium">{getColumnLabel(column)}</span>
                                <div class="flex shrink-0 items-center gap-1">
                                    <Button variant="ghost" size="icon-sm" onclick={() => moveColumnUp(column.id)} disabled={index === 0} title="Move up">
                                        <ChevronUp />
                                        <span class="sr-only">Move {getColumnLabel(column)} up</span>
                                    </Button>
                                    <Button
                                        variant="ghost"
                                        size="icon-sm"
                                        onclick={() => moveColumnDown(column.id)}
                                        disabled={index === visibleColumns.length - 1}
                                        title="Move down"
                                    >
                                        <ChevronDown />
                                        <span class="sr-only">Move {getColumnLabel(column)} down</span>
                                    </Button>
                                    {#if canRemoveColumn(column)}
                                        <Button
                                            variant="ghost"
                                            size="icon-sm"
                                            onclick={() => removeColumn(column)}
                                            title={`Remove ${getColumnLabel(column)} column`}
                                        >
                                            <X />
                                            <span class="sr-only">Remove {getColumnLabel(column)} column</span>
                                        </Button>
                                    {/if}
                                    <GripVertical class="text-muted-foreground/70" aria-hidden="true" />
                                </div>
                            </div>
                        {/each}
                    </div>
                </div>
            </section>
        </div>

        <Dialog.Footer class="mx-0 mb-0 rounded-b-xl px-6 py-4">
            <Button variant="outline" onclick={resetColumns}>
                <RotateCcw data-icon="inline-start" />
                Reset to default
            </Button>
            <Dialog.Close>
                {#snippet child({ props })}
                    <Button {...props}>Done</Button>
                {/snippet}
            </Dialog.Close>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
