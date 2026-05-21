<script module lang="ts">
    import type { RowData } from '@tanstack/svelte-table';

    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';
    import type { StockFeatures, Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { toFilter } from '$features/events/components/filters/helpers.svelte';
    import { serializeFilters } from '$features/events/components/filters/helpers.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import GripVertical from '@lucide/svelte/icons/grip-vertical';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import SlidersHorizontal from '@lucide/svelte/icons/sliders-horizontal';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import Undo2 from '@lucide/svelte/icons/undo-2';
    import { tick } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { NewSavedView, SavedView, UpdateSavedView } from '../models';

    import { deleteSavedView, markSavedViewDeleted, patchSavedView, postSavedView, restoreDeletedSavedView } from '../api.svelte';
    import DeleteViewDialog from './delete-view-dialog.svelte';
    import RenameViewDialog from './rename-view-dialog.svelte';
    import SaveViewDialog from './save-view-dialog.svelte';

    function getErrorMessage(error: unknown, fallback: string): string {
        const problem = error as ProblemDetails;
        const generalErrors = problem?.errors?.general;
        if (generalErrors?.[0]) {
            return generalErrors[0];
        }

        return problem?.title ?? fallback;
    }

    interface Props {
        activeSavedView?: SavedView;
        columnOrder?: string[];
        columnVisibility?: Record<string, boolean>;
        filters: IFilter[];
        isModified: boolean;
        onClearSavedView: () => void;
        onLoadView: (view: SavedView) => void;
        onResetToSaved: () => void;
        savedViews: SavedView[];
        setShowChart?: (show: boolean) => void;
        setShowStats?: (show: boolean) => void;
        showChart?: boolean;
        showStats?: boolean;
        sort?: string;
        table: Table<StockFeatures, TData>;
        time?: string;
        view: string;
    }

    let {
        activeSavedView,
        columnOrder,
        columnVisibility,
        filters,
        isModified,
        onClearSavedView,
        onLoadView,
        onResetToSaved,
        savedViews,
        setShowChart,
        setShowStats,
        showChart = true,
        showStats = true,
        sort,
        table,
        time,
        view
    }: Props = $props();

    let isSaveDialogOpen = $state(false);
    let isRenameDialogOpen = $state(false);
    let isDeleteDialogOpen = $state(false);
    let draggedColumnId = $state<null | string>(null);
    let viewToDelete = $state<null | SavedView>(null);

    const organizationId = $derived(organization.current);

    const createMutation = postSavedView({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });
    const updateMutation = patchSavedView({
        route: {
            get id() {
                return activeView?.id;
            }
        }
    });
    const removeMutation = deleteSavedView({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const saving = $derived(createMutation.isPending || updateMutation.isPending || removeMutation.isPending);

    const currentFilterString = $derived(toFilter(filters.filter((f) => f.type !== 'date')));

    // Auto-detect if current filters match an existing saved view for "load existing" hint
    const duplicateView = $derived.by(() => {
        if (activeSavedView || !currentFilterString) {
            return undefined;
        }

        return savedViews.find((savedView) => {
            if (savedView.filter !== currentFilterString) {
                return false;
            }

            if ((savedView.time ?? '') !== (time ?? '')) {
                return false;
            }

            if ((savedView.sort ?? '') !== (sort ?? '')) {
                return false;
            }

            return true;
        });
    });

    const activeView = $derived(activeSavedView);
    const hideableColumns = $derived(table.getAllLeafColumns().filter((column) => column.getCanHide()));
    const reorderableColumns = $derived(table.getAllLeafColumns().filter((column) => column.id !== 'select'));
    const visibleHideableColumnCount = $derived(hideableColumns.filter((column) => column.getIsVisible()).length);

    async function openSaveDialog() {
        await tick();
        isSaveDialogOpen = true;
    }

    async function openRenameDialog() {
        await tick();
        isRenameDialogOpen = true;
    }

    function canToggleColumn(column: (typeof hideableColumns)[number]): boolean {
        return !column.getIsVisible() || visibleHideableColumnCount > 1;
    }

    function toggleColumn(column: (typeof hideableColumns)[number]): void {
        if (canToggleColumn(column)) {
            column.toggleVisibility();
        }
    }

    function getColumnLabel(column: (typeof reorderableColumns)[number]): string {
        if (typeof column.columnDef.header === 'string') {
            return column.columnDef.header;
        }

        return column.id.replace(/[_-]/g, ' ').replace(/\w\S*/g, (word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase());
    }

    function getSavedColumnOrder(): string[] | undefined {
        const currentColumnIds = new Set(table.getAllLeafColumns().map((column) => column.id));
        const savedColumnOrder = (columnOrder ?? []).filter((columnId) => columnId !== 'select' && currentColumnIds.has(columnId));

        return savedColumnOrder.length > 0 ? savedColumnOrder : undefined;
    }

    function handleColumnDragEnd(): void {
        draggedColumnId = null;
    }

    function handleColumnDragOver(event: DragEvent, targetColumnId: string): void {
        event.preventDefault();
        if (event.dataTransfer) {
            event.dataTransfer.dropEffect = 'move';
        }

        if (draggedColumnId && draggedColumnId !== targetColumnId) {
            moveColumn(draggedColumnId, targetColumnId);
        }
    }

    function handleColumnDragStart(event: DragEvent, columnId: string): void {
        draggedColumnId = columnId;
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', columnId);
        }
    }

    function moveColumn(columnId: string, targetColumnId: string): void {
        const columnIds = reorderableColumns.map((column) => column.id);
        const columnIndex = columnIds.indexOf(columnId);
        const targetIndex = columnIds.indexOf(targetColumnId);
        if (columnIndex === -1 || targetIndex < 0 || targetIndex >= columnIds.length) {
            return;
        }

        const [movedColumnId] = columnIds.splice(columnIndex, 1);
        if (!movedColumnId) {
            return;
        }

        columnIds.splice(targetIndex, 0, movedColumnId);

        const allColumnIds = table.getAllLeafColumns().map((column) => column.id);
        const extraColumnIds = allColumnIds.filter((id) => id !== 'select' && !columnIds.includes(id));
        table.setColumnOrder(['select', ...columnIds, ...extraColumnIds]);
    }

    async function openDeleteDialog(savedView: SavedView) {
        viewToDelete = savedView;
        await tick();
        isDeleteDialogOpen = true;
    }

    async function handleSave(name: string, slug: string, isPrivate: boolean) {
        if (!organizationId) {
            return;
        }

        const filterDefinitions = serializeFilters(filters);
        const savedColumnOrder = getSavedColumnOrder();
        const body: NewSavedView = {
            column_order: savedColumnOrder,
            columns: columnVisibility,
            filter: currentFilterString || undefined,
            filter_definitions: filterDefinitions,
            is_private: isPrivate || undefined,
            name,
            organization_id: organizationId,
            show_chart: showChart,
            show_stats: showStats,
            slug,
            sort: sort || undefined,
            time: time || undefined,
            view_type: view
        };

        try {
            const result = await createMutation.mutateAsync(body);
            isSaveDialogOpen = false;
            onLoadView(result);
            toast.success(`Saved view "${result.name}" created.`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to save view. Please try again.'));
        }
    }

    async function handleRename(name: string, slug: string) {
        if (!activeView || !organizationId) {
            return;
        }

        try {
            const result = await updateMutation.mutateAsync({ name, slug });
            isRenameDialogOpen = false;
            toast.success(`View renamed to "${result.name}".`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to rename view. Please try again.'));
        }
    }

    async function handleUpdate() {
        if (!activeView || !organizationId) {
            return;
        }

        const body: UpdateSavedView = {
            column_order: getSavedColumnOrder() ?? null,
            columns: columnVisibility,
            filter: currentFilterString || null,
            filter_definitions: serializeFilters(filters),
            show_chart: showChart,
            show_stats: showStats,
            sort: sort || null,
            time: time || null
        };

        try {
            await updateMutation.mutateAsync(body);
            toast.success(`View "${activeView.name}" saved.`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to save view. Please try again.'));
        }
    }

    async function handleDelete() {
        if (!viewToDelete || !organizationId) {
            return;
        }

        const target = viewToDelete;
        const wasActiveView = activeSavedView?.id === target.id;
        markSavedViewDeleted(target);
        if (wasActiveView) {
            onClearSavedView();
        }

        try {
            await removeMutation.mutateAsync(target);

            toast.success(`View "${target.name}" deleted.`);
        } catch {
            restoreDeletedSavedView(target);
            if (wasActiveView) {
                onLoadView(target);
            }

            toast.error('Failed to delete view. Please try again.');
        } finally {
            isDeleteDialogOpen = false;
            viewToDelete = null;
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} class="relative gap-x-1.5 px-3" size="lg" variant="outline" title="Manage View Settings">
                <SlidersHorizontal class="size-4" aria-hidden="true" />
                <span>View</span>
                {#if isModified}
                    <span class="bg-primary absolute top-1 right-1 size-2 rounded-full" aria-label="Unsaved view changes"></span>
                {/if}
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end" class="w-64">
        <DropdownMenu.Group>
            <DropdownMenu.Label>Saved View</DropdownMenu.Label>
            {#if activeView}
                <DropdownMenu.Item disabled={saving || !isModified} onclick={handleUpdate}>
                    <Save class="mr-2 size-4" aria-hidden="true" />
                    Save
                </DropdownMenu.Item>
            {/if}
            <DropdownMenu.Item disabled={saving} onclick={openSaveDialog}>
                <Plus class="mr-2 size-4" aria-hidden="true" />
                Save As...
            </DropdownMenu.Item>
            {#if activeView}
                <DropdownMenu.Item disabled={saving} onclick={openRenameDialog}>
                    <Pencil class="mr-2 size-4" aria-hidden="true" />
                    Rename
                </DropdownMenu.Item>
                <DropdownMenu.Item disabled={!isModified} onclick={onResetToSaved}>
                    <Undo2 class="mr-2 size-4" aria-hidden="true" />
                    Reset to Saved
                </DropdownMenu.Item>
                <DropdownMenu.Separator />
                <DropdownMenu.Item class="text-destructive" onclick={() => openDeleteDialog(activeView)}>
                    <Trash2 class="mr-2 size-4" aria-hidden="true" />
                    Delete "{activeView.name}"
                </DropdownMenu.Item>
            {/if}
        </DropdownMenu.Group>
        {#if setShowStats || setShowChart}
            <DropdownMenu.Separator />
            <DropdownMenu.Group>
                <DropdownMenu.Label>Display</DropdownMenu.Label>
                {#if setShowStats}
                    <DropdownMenu.CheckboxItem
                        checked={showStats}
                        onclick={(event) => {
                            event.preventDefault();
                            setShowStats(!showStats);
                        }}
                        onSelect={(event) => event.preventDefault()}
                    >
                        Stat boxes
                    </DropdownMenu.CheckboxItem>
                {/if}
                {#if setShowChart}
                    <DropdownMenu.CheckboxItem
                        checked={showChart}
                        onclick={(event) => {
                            event.preventDefault();
                            setShowChart(!showChart);
                        }}
                        onSelect={(event) => event.preventDefault()}
                    >
                        Chart
                    </DropdownMenu.CheckboxItem>
                {/if}
            </DropdownMenu.Group>
        {/if}
        {#if reorderableColumns.length > 0}
            <DropdownMenu.Separator />
            <DropdownMenu.Group>
                <DropdownMenu.Label>Columns</DropdownMenu.Label>
                <div role="list">
                    {#each reorderableColumns as column (column.id)}
                        <div
                            class={[
                                'group/column flex cursor-grab items-center gap-1 rounded-md py-0.5 pr-1.5 active:cursor-grabbing',
                                draggedColumnId === column.id && 'bg-accent/70'
                            ]}
                            draggable="true"
                            ondragend={handleColumnDragEnd}
                            ondragover={(event) => handleColumnDragOver(event, column.id)}
                            ondragstart={(event) => handleColumnDragStart(event, column.id)}
                            role="listitem"
                        >
                            {#if column.getCanHide()}
                                <DropdownMenu.CheckboxItem
                                    checked={column.getIsVisible()}
                                    class="min-w-0 flex-1"
                                    disabled={!canToggleColumn(column)}
                                    onclick={(event) => {
                                        event.preventDefault();
                                        toggleColumn(column);
                                    }}
                                    onSelect={(event) => event.preventDefault()}
                                >
                                    <span class="truncate">{getColumnLabel(column)}</span>
                                </DropdownMenu.CheckboxItem>
                            {:else}
                                <span class="flex min-w-0 flex-1 items-center gap-1.5 px-1.5 py-1 text-sm">
                                    <span class="mr-2 size-4" aria-hidden="true"></span>
                                    <span class="truncate">{getColumnLabel(column)}</span>
                                </span>
                            {/if}
                            <GripVertical
                                class="text-muted-foreground/60 size-4 shrink-0 opacity-0 transition-opacity group-focus-within/column:opacity-100 group-hover/column:opacity-100"
                                aria-hidden="true"
                            />
                        </div>
                    {/each}
                </div>
            </DropdownMenu.Group>
        {/if}
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if isSaveDialogOpen}
    <SaveViewDialog
        bind:open={isSaveDialogOpen}
        {duplicateView}
        {savedViews}
        {saving}
        onSave={handleSave}
        onClose={() => (isSaveDialogOpen = false)}
        {onLoadView}
    />
{/if}

{#if isRenameDialogOpen && activeView}
    <RenameViewDialog
        bind:open={isRenameDialogOpen}
        name={activeView.name}
        slug={activeView.slug}
        viewId={activeView.id}
        {savedViews}
        {saving}
        onRename={handleRename}
        onClose={() => (isRenameDialogOpen = false)}
    />
{/if}

{#if isDeleteDialogOpen}
    <DeleteViewDialog bind:open={isDeleteDialogOpen} {viewToDelete} onDelete={handleDelete} />
{/if}
