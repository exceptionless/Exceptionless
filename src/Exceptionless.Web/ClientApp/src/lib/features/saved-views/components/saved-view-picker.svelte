<script module lang="ts">
    import type { RowData } from '@tanstack/svelte-table';

    type TData = RowData;
</script>

<script generics="TData extends RowData" lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';
    import type { StockFeatures, Table } from '@tanstack/svelte-table';

    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { toFilter } from '$features/events/components/filters/helpers.svelte';
    import { serializeFilters } from '$features/events/components/filters/helpers.svelte';
    import { getOrganizationQuery, getOrganizationsQuery } from '$features/organizations/api.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { createProductTourActions } from '$features/product-tours/actions.svelte';
    import ProductTourSpotlight from '$features/product-tours/components/product-tour-spotlight.svelte';
    import { productTourCheckpoint } from '$features/product-tours/state.svelte';
    import { supportsColumnWrapping } from '$features/shared/components/data-table/column-meta';
    import { getMeQuery } from '$features/users/api.svelte';
    import Building2 from '@lucide/svelte/icons/building-2';
    import Columns3 from '@lucide/svelte/icons/columns-3';
    import House from '@lucide/svelte/icons/house';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import SlidersHorizontal from '@lucide/svelte/icons/sliders-horizontal';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import Undo2 from '@lucide/svelte/icons/undo-2';
    import { tick } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { AutoFillColumnSelection, WrappedColumnIds } from '../column-settings';
    import type { NewSavedView, SavedView, UpdateSavedView } from '../models';

    import {
        deleteSavedView,
        markSavedViewDeleted,
        patchSavedView,
        postSavedView,
        putOrganizationSavedViewDefault,
        putUserSavedViewDefault,
        restoreDeletedSavedView
    } from '../api.svelte';
    import { buildColumnSettings } from '../column-settings';
    import { resolveSavedViewDefaults } from '../defaults';
    import ColumnManagementDialog from './column-management-dialog.svelte';
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
        autoFillColumnId: AutoFillColumnSelection;
        canModifySavedView?: boolean;
        columnOrder?: string[];
        columnSizing?: Record<string, number>;
        columnVisibility?: Record<string, boolean>;
        defaultAutoFillColumnId?: string;
        filters: IFilter[];
        isModified: boolean;
        onClearSavedView: () => Promise<void>;
        onLoadView: (view: SavedView) => Promise<void> | void;
        onResetToSaved: () => void;
        onSavedViewUpdated: (view: SavedView) => void;
        savedViews: SavedView[];
        setAutoFillColumnId: (columnId: AutoFillColumnSelection) => void;
        setShowChart?: (show: boolean) => void;
        setShowStats?: (show: boolean) => void;
        setWrappedColumnIds: (columnIds: WrappedColumnIds) => void;
        showChart?: boolean;
        showStats?: boolean;
        sort?: string;
        table: Table<StockFeatures, TData>;
        time?: string;
        view: string;
        wrappedColumnIds: WrappedColumnIds;
    }

    let {
        activeSavedView,
        autoFillColumnId,
        canModifySavedView = true,
        columnOrder,
        columnSizing,
        columnVisibility,
        defaultAutoFillColumnId,
        filters,
        isModified,
        onClearSavedView,
        onLoadView,
        onResetToSaved,
        onSavedViewUpdated,
        savedViews,
        setAutoFillColumnId,
        setShowChart,
        setShowStats,
        setWrappedColumnIds,
        showChart = true,
        showStats = true,
        sort,
        table,
        time,
        view,
        wrappedColumnIds
    }: Props = $props();

    let isSaveDialogOpenManually = $state(false);
    let isRenameDialogOpen = $state(false);
    let isDeleteDialogOpen = $state(false);
    let isColumnDialogOpen = $state(false);
    let isMenuOpen = $state(false);
    let viewToDelete = $state<null | SavedView>(null);
    const tourActions = createProductTourActions();
    const savedViewCheckpoint = $derived(productTourCheckpoint.current?.tourName === 'create-saved-view' ? productTourCheckpoint.current : undefined);
    const isSaveDialogOpen = $derived(
        isSaveDialogOpenManually || savedViewCheckpoint?.phase.type === 'saved-view-created' || savedViewCheckpoint?.phase.type === 'saved-view-loaded'
    );
    const pendingTourView = $derived.by(() => {
        const phase = savedViewCheckpoint?.phase;
        return phase?.type === 'saved-view-created' || phase?.type === 'saved-view-loaded'
            ? savedViews.find((savedView) => savedView.id === phase.viewId)
            : undefined;
    });

    const organizationId = $derived(organization.current);
    const activeView = $derived(activeSavedView);
    const currentUserQuery = getMeQuery();
    const organizationsQuery = getOrganizationsQuery({});
    const membershipOrganization = $derived(organizationsQuery.data?.data?.find((organizationItem) => organizationItem.id === organizationId));
    const organizationIdToLoad = $derived(organizationsQuery.isSuccess && !membershipOrganization ? organizationId : undefined);
    const currentOrganizationQuery = getOrganizationQuery({
        route: {
            get id() {
                return organizationIdToLoad;
            }
        }
    });
    const currentOrganization = $derived(membershipOrganization ?? currentOrganizationQuery.data);
    const defaults = $derived.by(() => {
        return resolveSavedViewDefaults({
            organizationDefaultSavedViewId: currentOrganization?.default_saved_view_id,
            organizationId,
            organizationPreferences: currentUserQuery.data?.organization_preferences,
            savedViews
        });
    });

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
    const userDefaultMutation = putUserSavedViewDefault({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });
    const organizationDefaultMutation = putOrganizationSavedViewDefault({
        route: {
            get organizationId() {
                return organizationId;
            }
        }
    });

    const saving = $derived(
        createMutation.isPending ||
            updateMutation.isPending ||
            removeMutation.isPending ||
            userDefaultMutation.isPending ||
            organizationDefaultMutation.isPending
    );
    const isUserDefault = $derived(!!activeView && defaults.userDefault?.id === activeView.id);
    const isOrganizationDefault = $derived(!!activeView && defaults.organizationDefault?.id === activeView.id);
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

    const reorderableColumns = $derived(table.getAllLeafColumns().filter((column) => column.id !== 'select'));

    async function openSaveDialog() {
        await tick();
        isSaveDialogOpenManually = true;
    }

    async function openRenameDialog() {
        await tick();
        isRenameDialogOpen = true;
    }

    function getSavedColumnSettings() {
        const supportedWrappedColumnIds = wrappedColumnIds.filter((columnId) => supportsColumnWrapping(table.getColumn(columnId)?.columnDef.meta));
        return buildColumnSettings(
            table.getAllLeafColumns().map((column) => column.id),
            columnOrder ?? [],
            columnVisibility ?? {},
            columnSizing ?? {},
            autoFillColumnId,
            defaultAutoFillColumnId,
            supportedWrappedColumnIds
        );
    }

    async function openDeleteDialog(savedView: SavedView) {
        viewToDelete = savedView;
        await tick();
        isDeleteDialogOpen = true;
    }

    function handleResetToSaved(): void {
        if (!canModifySavedView) {
            return;
        }

        isMenuOpen = false;
        onResetToSaved();
    }

    async function handleSave(name: string, slug: string, isPrivate: boolean) {
        if (!organizationId) {
            return;
        }

        const checkpoint = savedViewCheckpoint;
        if (checkpoint?.phase.type === 'saved-view-loaded') {
            if (await tourActions.complete(checkpoint)) {
                isSaveDialogOpenManually = false;
            }
            return;
        }

        if (checkpoint?.phase.type === 'saved-view-created') {
            if (!pendingTourView) {
                toast.error('The created view could not be loaded. Refresh and try again.');
                return;
            }

            try {
                await onLoadView(pendingTourView);
                const loadedCheckpoint = productTourCheckpoint.advance(checkpoint, 'view-created', {
                    type: 'saved-view-loaded',
                    viewId: checkpoint.phase.viewId
                });
                if (loadedCheckpoint && (await tourActions.complete(loadedCheckpoint))) {
                    isSaveDialogOpenManually = false;
                }
            } catch (error) {
                toast.error(getErrorMessage(error, 'Failed to load the created view. Please try again.'));
            }
            return;
        }

        const filterDefinitions = serializeFilters(filters);
        const body: NewSavedView = {
            columns: getSavedColumnSettings(),
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
            if (checkpoint) {
                const createdCheckpoint = productTourCheckpoint.advance(checkpoint, 'view-created', {
                    type: 'saved-view-created',
                    viewId: result.id
                });
                await onLoadView(result);
                const loadedCheckpoint = createdCheckpoint
                    ? productTourCheckpoint.advance(createdCheckpoint, 'view-created', {
                          type: 'saved-view-loaded',
                          viewId: result.id
                      })
                    : undefined;
                if (loadedCheckpoint && (await tourActions.complete(loadedCheckpoint))) {
                    isSaveDialogOpenManually = false;
                }
            } else {
                isSaveDialogOpenManually = false;
                await onLoadView(result);
            }
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
            const result = await updateMutation.mutateAsync({
                name,
                slug
            });
            isRenameDialogOpen = false;
            toast.success(`View renamed to "${result.name}".`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to rename view. Please try again.'));
        }
    }

    function getUpdateBody(): UpdateSavedView {
        return {
            columns: getSavedColumnSettings(),
            filter: currentFilterString || null,
            filter_definitions: serializeFilters(filters),
            show_chart: showChart,
            show_stats: showStats,
            sort: sort || null,
            time: time || null
        };
    }

    async function handleUpdate() {
        if (!activeView || !organizationId || !canModifySavedView) {
            return;
        }

        try {
            const result = await updateMutation.mutateAsync(getUpdateBody());
            onSavedViewUpdated(result);
            toast.success(`View "${activeView.name}" saved.`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to save view. Please try again.'));
        }
    }

    async function toggleUserDefault(): Promise<void> {
        if (!activeView || !organizationId) {
            return;
        }

        const clearingDefault = isUserDefault;
        try {
            await userDefaultMutation.mutateAsync({
                saved_view_id: clearingDefault ? null : activeView.id
            });
            toast.success(clearingDefault ? 'Personal home view cleared.' : `"${activeView.name}" is now your home view.`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to update your home view. Please try again.'));
        }
    }

    async function toggleOrganizationDefault(): Promise<void> {
        if (!activeView || activeView.user_id || !organizationId) {
            return;
        }

        const clearingDefault = isOrganizationDefault;
        try {
            await organizationDefaultMutation.mutateAsync({
                saved_view_id: clearingDefault ? null : activeView.id
            });
            toast.success(clearingDefault ? 'Organization home view cleared.' : `"${activeView.name}" is now the organization home view.`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to update the organization home view. Please try again.'));
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
            await onClearSavedView();
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

<DropdownMenu.Root bind:open={isMenuOpen}>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} class="relative gap-x-1.5 px-3" data-tour="saved-view-trigger" size="lg" variant="outline" title="Manage View Settings">
                <SlidersHorizontal class="size-4" aria-hidden="true" />
                <span>View</span>
                {#if isModified}
                    <span class="bg-primary absolute top-1 right-1 size-2 rounded-full" aria-label="Unsaved view changes"></span>
                {/if}
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end" class="w-64" data-tour="saved-view-settings">
        <DropdownMenu.Group>
            <DropdownMenu.Label>Saved View</DropdownMenu.Label>
            {#if activeView}
                <DropdownMenu.Item disabled={saving || !isModified || !canModifySavedView} onclick={handleUpdate}>
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
                <DropdownMenu.Item disabled={!isModified || !canModifySavedView} onclick={handleResetToSaved}>
                    <Undo2 class="mr-2 size-4" aria-hidden="true" />
                    Reset to Saved
                </DropdownMenu.Item>
            {/if}
        </DropdownMenu.Group>
        {#if activeView}
            <DropdownMenu.Separator />
            <DropdownMenu.Group>
                <DropdownMenu.Label>Home</DropdownMenu.Label>
                <DropdownMenu.Item disabled={saving} onclick={toggleUserDefault}>
                    <House class="mr-2 size-4" aria-hidden="true" />
                    {isUserDefault ? 'Clear my home view' : 'Set as my home view'}
                </DropdownMenu.Item>
                {#if !activeView.user_id}
                    <DropdownMenu.Item disabled={saving} onclick={toggleOrganizationDefault}>
                        <Building2 class="mr-2 size-4" aria-hidden="true" />
                        {isOrganizationDefault ? 'Clear organization home' : 'Set as organization home'}
                    </DropdownMenu.Item>
                {/if}
            </DropdownMenu.Group>
            <DropdownMenu.Separator />
            <DropdownMenu.Group>
                <DropdownMenu.Item class="text-destructive" onclick={() => openDeleteDialog(activeView)}>
                    <Trash2 class="mr-2 size-4" aria-hidden="true" />
                    Delete "{activeView.name}"
                </DropdownMenu.Item>
            </DropdownMenu.Group>
        {/if}
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
                <DropdownMenu.Item onclick={() => (isColumnDialogOpen = true)}>
                    <Columns3 class="mr-2 size-4" aria-hidden="true" />
                    Manage Columns...
                </DropdownMenu.Item>
            </DropdownMenu.Group>
        {/if}
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if isSaveDialogOpen}
    <SaveViewDialog
        open={isSaveDialogOpen}
        defaultPrivate={!!savedViewCheckpoint}
        {duplicateView}
        onCancel={async () => {
            if (savedViewCheckpoint) {
                await tourActions.dismiss(savedViewCheckpoint);
            }
        }}
        {savedViews}
        {saving}
        onSave={handleSave}
        onClose={() => (isSaveDialogOpenManually = false)}
        onTourContinue={(checkpointName) => {
            const checkpoint = savedViewCheckpoint;
            if (checkpoint) {
                productTourCheckpoint.advance(checkpoint, checkpointName);
            }
        }}
        pendingCompletion={savedViewCheckpoint?.phase.type === 'saved-view-created' || savedViewCheckpoint?.phase.type === 'saved-view-loaded'}
        tourCheckpointName={savedViewCheckpoint?.checkpointName}
        {onLoadView}
    />
{/if}

{#if savedViewCheckpoint?.checkpointName === 'open-view-menu'}
    <ProductTourSpotlight
        checkpoint={savedViewCheckpoint}
        description="Open View settings to review what a reusable saved view can remember."
        onDismiss={tourActions.dismiss}
        onNext={(checkpoint) => {
            isMenuOpen = true;
            productTourCheckpoint.advance(checkpoint, 'review-settings');
        }}
        target="[data-tour='saved-view-trigger']"
        title="Open View settings"
    />
{:else if savedViewCheckpoint?.checkpointName === 'review-settings'}
    <ProductTourSpotlight
        checkpoint={savedViewCheckpoint}
        description="Review the filters, date range, display choices, and columns. The guide will not change them for you."
        onDismiss={tourActions.dismiss}
        onNext={(checkpoint) => {
            isMenuOpen = false;
            isSaveDialogOpenManually = true;
            productTourCheckpoint.advance(checkpoint, 'name-view');
        }}
        target="[data-tour='saved-view-settings']"
        title="Configure what the view remembers"
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

{#if isColumnDialogOpen}
    <ColumnManagementDialog
        bind:open={isColumnDialogOpen}
        {autoFillColumnId}
        {defaultAutoFillColumnId}
        {setAutoFillColumnId}
        {setWrappedColumnIds}
        {table}
        {wrappedColumnIds}
    />
{/if}
