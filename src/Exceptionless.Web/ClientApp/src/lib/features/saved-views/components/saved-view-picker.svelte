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
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import SlidersHorizontal from '@lucide/svelte/icons/sliders-horizontal';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import { tick } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { NewSavedView, SavedView, UpdateSavedView } from '../models';

    import { deleteSavedView, patchSavedView, postSavedView } from '../api.svelte';
    import DeleteViewDialog from './delete-view-dialog.svelte';
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
        columnVisibility?: Record<string, boolean>;
        filters: IFilter[];
        isModified: boolean;
        onClearSavedView: () => void;
        onLoadView: (id: string) => void;
        savedViews: SavedView[];
        sort?: string;
        table: Table<StockFeatures, TData>;
        time?: string;
        view: string;
    }

    let { activeSavedView, columnVisibility, filters, isModified, onClearSavedView, onLoadView, savedViews, sort, table, time, view }: Props = $props();

    let isSaveDialogOpen = $state(false);
    let isDeleteDialogOpen = $state(false);
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

    async function openSaveDialog() {
        await tick();
        isSaveDialogOpen = true;
    }

    async function openDeleteDialog(savedView: SavedView) {
        viewToDelete = savedView;
        await tick();
        isDeleteDialogOpen = true;
    }

    async function handleSave(name: string, isPrivate: boolean, isDefault: boolean) {
        if (!organizationId) {
            return;
        }

        const filterDefinitions = serializeFilters(filters);
        const body: NewSavedView = {
            columns: columnVisibility,
            filter: currentFilterString || undefined,
            filter_definitions: filterDefinitions,
            is_default: isDefault || undefined,
            is_private: isPrivate || undefined,
            name,
            organization_id: organizationId,
            sort: sort || undefined,
            time: time || undefined,
            view_type: view
        };

        try {
            const result = await createMutation.mutateAsync(body);
            isSaveDialogOpen = false;
            onLoadView(result.id);
            toast.success(`Saved view "${result.name}" created.`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to save view. Please try again.'));
        }
    }

    async function handleUpdate() {
        if (!activeView || !organizationId) {
            return;
        }

        const body: UpdateSavedView = {
            columns: columnVisibility,
            filter: currentFilterString || null,
            filter_definitions: serializeFilters(filters),
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
        try {
            await removeMutation.mutateAsync(target);

            if (activeSavedView?.id === target.id) {
                onClearSavedView();
            }

            toast.success(`View "${target.name}" deleted.`);
        } catch {
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
            <Button {...props} class="gap-x-1.5 px-3" size="lg" variant="outline" title="Manage view settings">
                <SlidersHorizontal class="size-4" aria-hidden="true" />
                <span>View</span>
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
                <DropdownMenu.Separator />
                <DropdownMenu.Item class="text-destructive" onclick={() => openDeleteDialog(activeView)}>
                    <Trash2 class="mr-2 size-4" aria-hidden="true" />
                    Delete "{activeView.name}"
                </DropdownMenu.Item>
            {/if}
        </DropdownMenu.Group>
        {#if hideableColumns.length > 0}
            <DropdownMenu.Separator />
            <DropdownMenu.Group>
                <DropdownMenu.Label>Columns</DropdownMenu.Label>
                {#each hideableColumns as column (column.id)}
                    <DropdownMenu.CheckboxItem checked={column.getIsVisible()} onclick={() => column.toggleVisibility()}>
                        {column.columnDef.header}
                    </DropdownMenu.CheckboxItem>
                {/each}
            </DropdownMenu.Group>
        {/if}
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if isSaveDialogOpen}
    <SaveViewDialog bind:open={isSaveDialogOpen} {duplicateView} {saving} onSave={handleSave} onClose={() => (isSaveDialogOpen = false)} {onLoadView} />
{/if}

{#if isDeleteDialogOpen}
    <DeleteViewDialog bind:open={isDeleteDialogOpen} {viewToDelete} onDelete={handleDelete} />
{/if}
