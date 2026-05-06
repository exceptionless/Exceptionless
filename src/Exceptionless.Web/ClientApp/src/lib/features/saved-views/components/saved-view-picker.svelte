<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { Muted, P } from '$comp/typography';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import * as Tooltip from '$comp/ui/tooltip';
    import { toFilter } from '$features/events/components/filters/helpers.svelte';
    import { serializeFilters } from '$features/events/components/filters/helpers.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { quickRanges } from '$features/shared/components/date-range-picker/quick-ranges';
    import Check from '@lucide/svelte/icons/check';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import Star from '@lucide/svelte/icons/star';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import Undo2 from '@lucide/svelte/icons/undo-2';
    import { tick } from 'svelte';
    import { toast } from 'svelte-sonner';
    import { SvelteMap } from 'svelte/reactivity';

    import type { NewSavedView, SavedView, UpdateSavedView } from '../models';

    import { deleteSavedView, patchSavedView, postSavedView } from '../api.svelte';
    import DeleteViewDialog from './delete-view-dialog.svelte';
    import RenameViewDialog from './rename-view-dialog.svelte';
    import SaveViewDialog from './save-view-dialog.svelte';

    const timeLabels = new SvelteMap<string, string>();
    for (const section of quickRanges) {
        for (const option of section.options) {
            timeLabels.set(option.value, option.label);
        }
    }

    function formatViewSummary(savedView: SavedView): string {
        const parts: string[] = [];

        if (savedView.filter) {
            parts.push(savedView.filter);
        }

        if (savedView.time) {
            parts.push(timeLabels.get(savedView.time) ?? savedView.time);
        }

        return parts.join(' · ') || 'No filters';
    }

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
        onResetToSaved: () => void;
        savedViews: SavedView[];
        time?: string;
        view: string;
    }

    let { activeSavedView, columnVisibility, filters, isModified, onClearSavedView, onLoadView, onResetToSaved, savedViews, time, view }: Props = $props();

    let isSaveDialogOpen = $state(false);
    let isRenameDialogOpen = $state(false);
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

    const sortedViews = $derived.by(() => {
        return [...savedViews].sort((left, right) => {
            if (left.is_default && !right.is_default) {
                return -1;
            }

            if (!left.is_default && right.is_default) {
                return 1;
            }

            return left.name.localeCompare(right.name);
        });
    });

    // Auto-detect if current filters match an existing saved view for "load existing" hint
    const duplicateView = $derived.by(() => {
        if (activeSavedView || !currentFilterString) {
            return undefined;
        }

        return savedViews.find((savedView) => {
            if (savedView.filter !== currentFilterString) {
                return false;
            }

            if (savedView.time && (time ?? '') !== savedView.time) {
                return false;
            }

            return true;
        });
    });

    const activeView = $derived(activeSavedView);

    async function openSaveDialog() {
        await tick();
        isSaveDialogOpen = true;
    }

    async function openRenameDialog() {
        if (!activeView) {
            return;
        }

        await tick();
        isRenameDialogOpen = true;
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

    async function handleUpdateFilters() {
        if (!activeSavedView || !organizationId) {
            return;
        }

        const filterDefinitions = serializeFilters(filters);
        const body: UpdateSavedView = {
            columns: columnVisibility,
            filter: currentFilterString || null,
            filter_definitions: filterDefinitions,
            time: time || null
        };

        try {
            await updateMutation.mutateAsync(body);
            toast.success(`View "${activeSavedView.name}" updated.`);
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to update view. Please try again.'));
        }
    }

    async function handleRename(newName: string) {
        if (!activeView || !organizationId) {
            return;
        }

        try {
            await updateMutation.mutateAsync({ name: newName });
            isRenameDialogOpen = false;
            toast.success('View renamed.');
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to rename view. Please try again.'));
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
                // Navigate to remaining default view, or fall back to clearing the state
                const remainingViews = savedViews.filter((v) => v.id !== target.id);
                const newDefault = remainingViews.find((v) => v.is_default);
                if (newDefault) {
                    onLoadView(newDefault.id);
                } else {
                    onClearSavedView();
                }
            }

            toast.success(`View "${target.name}" deleted.`);
        } catch {
            toast.error('Failed to delete view. Please try again.');
        } finally {
            isDeleteDialogOpen = false;
            viewToDelete = null;
        }
    }

    function handleSelect(savedView: SavedView) {
        onLoadView(savedView.id);
    }

    async function handleToggleDefault() {
        if (!activeView || !organizationId) {
            return;
        }

        try {
            await updateMutation.mutateAsync({ is_default: true });
            toast.success('Set as default.');
        } catch (error) {
            toast.error(getErrorMessage(error, 'Failed to update default setting.'));
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} class="gap-x-1.5 px-3" size="lg" variant="outline">
                <Save class="size-4" />
                {#if activeView}
                    <span class="max-w-37.5 truncate">{activeView.name}</span>
                    {#if isModified}
                        <Badge variant="secondary" class="px-1.5 py-0 text-[10px]">modified</Badge>
                    {/if}
                {:else}
                    <span>Saved Views</span>
                {/if}
                <ChevronDown class="size-3.5 opacity-50" />
            </Button>
        {/snippet}
    </DropdownMenu.Trigger>
    <DropdownMenu.Content align="end" class="w-72">
        {#if activeSavedView && isModified}
            <DropdownMenu.Group>
                <DropdownMenu.GroupHeading>Modified View</DropdownMenu.GroupHeading>
                <DropdownMenu.Item disabled={saving} onclick={handleUpdateFilters}>
                    <Save class="mr-2 size-4" />
                    Update "{activeSavedView.name}"
                </DropdownMenu.Item>
                <DropdownMenu.Item onclick={openSaveDialog}>
                    <Plus class="mr-2 size-4" />
                    Save as new view
                </DropdownMenu.Item>
                <DropdownMenu.Item onclick={onResetToSaved}>
                    <Undo2 class="mr-2 size-4" />
                    Reset to saved
                </DropdownMenu.Item>
            </DropdownMenu.Group>
            <DropdownMenu.Separator />
        {/if}

        {#if savedViews.length > 0}
            <DropdownMenu.Group>
                <DropdownMenu.GroupHeading>Saved Views</DropdownMenu.GroupHeading>
                {#each sortedViews as savedView (savedView.id)}
                    <DropdownMenu.Item class="flex items-center justify-between" onclick={() => handleSelect(savedView)}>
                        <span class="flex min-w-0 items-start gap-2">
                            <span class="flex size-4 shrink-0 items-center justify-center pt-0.5">
                                {#if activeView?.id === savedView.id}
                                    <Check class="size-3.5" />
                                {/if}
                            </span>
                            <span class="flex min-w-0 flex-col">
                                <span class="flex items-center gap-1.5">
                                    <span class="truncate">{savedView.name}</span>
                                    {#if savedView.is_default}
                                        <Badge variant="secondary" class="px-1 py-0 text-[10px]">default</Badge>
                                    {/if}
                                    {#if savedView.user_id}
                                        <Badge variant="outline" class="px-1 py-0 text-[10px]">private</Badge>
                                    {/if}
                                </span>
                                {#if savedView.filter || savedView.time}
                                    <Tooltip.Root>
                                        <Tooltip.Trigger>
                                            {#snippet child({ props: tipProps })}
                                                <span {...tipProps} class="text-muted-foreground max-w-50 truncate text-left text-[11px]">
                                                    {formatViewSummary(savedView)}
                                                </span>
                                            {/snippet}
                                        </Tooltip.Trigger>
                                        <Tooltip.Content class="max-w-xs" side="right">
                                            {#if savedView.filter}
                                                <P class="font-mono text-xs">{savedView.filter}</P>
                                            {/if}
                                            {#if savedView.time}
                                                <Muted class="text-xs">{timeLabels.get(savedView.time) ?? savedView.time}</Muted>
                                            {/if}
                                        </Tooltip.Content>
                                    </Tooltip.Root>
                                {:else}
                                    <Muted class="text-left text-[11px]">No filters</Muted>
                                {/if}
                            </span>
                        </span>
                        <Button
                            variant="ghost"
                            size="icon"
                            class="text-muted-foreground hover:text-destructive -mr-1 size-5 shrink-0"
                            aria-label="Delete {savedView.name}"
                            onclick={(e) => {
                                e.stopPropagation();
                                openDeleteDialog(savedView);
                            }}
                        >
                            <Trash2 class="size-3" aria-hidden="true" />
                        </Button>
                    </DropdownMenu.Item>
                {/each}
            </DropdownMenu.Group>
            <DropdownMenu.Separator />
        {/if}

        <DropdownMenu.Group>
            {#if duplicateView && !activeSavedView}
                <DropdownMenu.Item onclick={() => handleSelect(duplicateView)}>
                    <Check class="mr-2 size-4" />
                    Load "{duplicateView.name}" (matches current)
                </DropdownMenu.Item>
            {/if}
            {#if !activeView}
                <DropdownMenu.Item onclick={openSaveDialog}>
                    <Plus class="mr-2 size-4" />
                    Save current view
                </DropdownMenu.Item>
            {/if}
            {#if activeView}
                <DropdownMenu.Item onclick={openRenameDialog}>
                    <Pencil class="mr-2 size-4" />
                    Rename "{activeView.name}"
                </DropdownMenu.Item>
                {#if !activeView.user_id && !activeView.is_default}
                    <DropdownMenu.Item disabled={saving} onclick={handleToggleDefault}>
                        <Star class="mr-2 size-4" />
                        Set as default for everyone
                    </DropdownMenu.Item>
                {/if}
                <DropdownMenu.Item onclick={onClearSavedView}>Clear Saved View</DropdownMenu.Item>
                <DropdownMenu.Separator />
                <DropdownMenu.Item class="text-destructive" onclick={() => openDeleteDialog(activeView)}>
                    <Trash2 class="mr-2 size-4" />
                    Delete "{activeView.name}"
                </DropdownMenu.Item>
            {/if}
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if isSaveDialogOpen}
    <SaveViewDialog bind:open={isSaveDialogOpen} {duplicateView} {saving} onSave={handleSave} onClose={() => (isSaveDialogOpen = false)} {onLoadView} />
{/if}

{#if isRenameDialogOpen && activeView}
    <RenameViewDialog bind:open={isRenameDialogOpen} name={activeView.name} {saving} onRename={handleRename} onClose={() => (isRenameDialogOpen = false)} />
{/if}

{#if isDeleteDialogOpen}
    <DeleteViewDialog bind:open={isDeleteDialogOpen} {viewToDelete} onDelete={handleDelete} />
{/if}
