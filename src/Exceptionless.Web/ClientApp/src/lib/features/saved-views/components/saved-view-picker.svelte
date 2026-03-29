<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';

    import * as AlertDialog from '$comp/ui/alert-dialog';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import * as DropdownMenu from '$comp/ui/dropdown-menu';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import { Switch } from '$comp/ui/switch';
    import * as Tooltip from '$comp/ui/tooltip';
    import { toFilter } from '$features/events/components/filters/helpers.svelte';
    import { serializeFilters } from '$features/events/components/filters/helpers.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { quickRanges } from '$features/shared/components/date-range-picker/quick-ranges';
    import { useFetchClient } from '@exceptionless/fetchclient';
    import Check from '@lucide/svelte/icons/check';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import Star from '@lucide/svelte/icons/star';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import Undo2 from '@lucide/svelte/icons/undo-2';
    import { useQueryClient } from '@tanstack/svelte-query';
    import { tick, untrack } from 'svelte';
    import { toast } from 'svelte-sonner';
    import { SvelteMap } from 'svelte/reactivity';

    import type { NewSavedView, SavedView, UpdateSavedView } from '../models';

    import { queryKeys } from '../api.svelte';

    // Map date-math time values to friendly labels
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

    let saveDialogOpen = $state(false);
    let renameDialogOpen = $state(false);
    let deleteDialogOpen = $state(false);
    let deleteTarget = $state<null | SavedView>(null);
    let saveName = $state('');
    let savePrivate = $state(false);
    let saveAsDefault = $state(false);
    let renameName = $state('');
    let saving = $state(false);

    const client = useFetchClient();
    const queryClient = useQueryClient();
    const organizationId = $derived(organization.current);

    // Compute current filter string for matching
    const currentFilterString = $derived(toFilter(filters.filter((f) => f.type !== 'date')));

    // Sort: default view first, then alphabetically by name
    const sortedViews = $derived.by(() => {
        return [...savedViews].sort((a, b) => {
            if (a.is_default && !b.is_default) {
                return -1;
            }
            if (!a.is_default && b.is_default) {
                return 1;
            }
            return a.name.localeCompare(b.name);
        });
    });

    // Auto-detect if current filters match an existing saved view (even without ?saved=)
    const matchingSavedView = $derived.by(() => {
        if (activeSavedView) {
            return undefined;
        }
        if (!currentFilterString) {
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

    // The effective filter (explicitly loaded OR auto-matched)
    const effectiveView = $derived(activeSavedView ?? matchingSavedView);

    // When a view is auto-matched (filter matches without an explicit ?saved= param),
    // update the URL so the sidebar highlights the correct view.
    $effect(() => {
        const matched = matchingSavedView;
        if (matched) {
            untrack(() => onLoadView(matched.id));
        }
    });

    // Check if saving would duplicate an existing filter
    const duplicateView = $derived.by(() => {
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

    async function openSaveDialog() {
        saveName = '';
        savePrivate = false;
        saveAsDefault = false;
        await tick();
        saveDialogOpen = true;
    }

    async function openRenameDialog() {
        if (effectiveView) {
            renameName = effectiveView.name;
            await tick();
            renameDialogOpen = true;
        }
    }

    async function openDeleteDialog(savedView: SavedView) {
        deleteTarget = savedView;
        await tick();
        deleteDialogOpen = true;
    }

    async function handleSave() {
        if (!organizationId || !saveName.trim()) {
            return;
        }

        saving = true;
        try {
            const filterDefinitions = serializeFilters(filters);
            const body: NewSavedView = {
                columns: columnVisibility,
                filter: currentFilterString || undefined,
                filter_definitions: filterDefinitions,
                is_default: saveAsDefault || undefined,
                name: saveName.trim(),
                organization_id: organizationId,
                time: time || undefined,
                view
            };
            const url = savePrivate ? `organizations/${organizationId}/saved-views?is_private=true` : `organizations/${organizationId}/saved-views`;
            const response = await client.postJSON<SavedView>(url, body, { expectedStatusCodes: [422] });
            if (response.ok && response.data) {
                const savedView = response.data;
                // Optimistically add to per-view cache so activeSavedView resolves
                // immediately when onLoadView fires, preventing matchingSavedView
                // from incorrectly auto-matching an existing view with the same filter.
                // We do NOT fire background invalidation here — the Elasticsearch index has
                // a refresh delay (~1s), so a refetch would return stale data that omits the
                // new view, which would trigger the "not found" effect and clear the URL param.
                // The cache stays current via WebSocket events or the user's next navigation.
                queryClient.setQueryData(queryKeys.view(organizationId, view), (old: SavedView[] | undefined) => (old ? [...old, savedView] : [savedView]));
                saveDialogOpen = false;
                onLoadView(savedView.id);
                toast.success(`Saved view "${savedView.name}" created.`);
            } else {
                const message = response.problem?.title ?? 'Failed to save view. Please try again.';
                toast.error(message);
            }
        } catch {
            toast.error('An error occurred while saving the view.');
        } finally {
            saving = false;
        }
    }

    async function handleUpdate() {
        if (!activeSavedView || !organizationId) {
            return;
        }

        saving = true;
        try {
            const filterDefinitions = serializeFilters(filters);
            const body: UpdateSavedView = {
                columns: columnVisibility,
                filter: currentFilterString || null,
                filter_definitions: filterDefinitions,
                time: time || null
            };
            const response = await client.patchJSON(`saved-views/${activeSavedView.id}`, body, { expectedStatusCodes: [422] });
            if (response.ok) {
                // Optimistically update the cache with the new filter/time values so the
                // hydration effect doesn't revert the user's changes when the background
                // refetch returns stale Elasticsearch data.
                queryClient.setQueryData(
                    queryKeys.view(organizationId, view),
                    (old: SavedView[] | undefined) =>
                        old?.map((v) =>
                            v.id === activeSavedView.id
                                ? {
                                      ...v,
                                      columns: body.columns ?? v.columns,
                                      filter: body.filter !== undefined ? body.filter : v.filter,
                                      filter_definitions: body.filter_definitions !== undefined ? body.filter_definitions : v.filter_definitions,
                                      time: body.time !== undefined ? body.time : v.time
                                  }
                                : v
                        ) ?? []
                );
                toast.success(`View "${activeSavedView.name}" updated.`);
            } else {
                const message = response.problem?.title ?? 'Failed to update view. Please try again.';
                toast.error(message);
            }
        } catch {
            toast.error('An error occurred while updating the view.');
        } finally {
            saving = false;
        }
    }

    async function handleRename() {
        if (!effectiveView || !organizationId || !renameName.trim()) {
            return;
        }

        saving = true;
        try {
            const body: UpdateSavedView = { name: renameName.trim() };
            const response = await client.patchJSON(`saved-views/${effectiveView.id}`, body, { expectedStatusCodes: [422] });
            if (response.ok) {
                renameDialogOpen = false;
                void queryClient.invalidateQueries({ queryKey: queryKeys.type });
                toast.success('View renamed.');
            } else {
                const message = response.problem?.title ?? 'Failed to rename view. Please try again.';
                toast.error(message);
            }
        } catch {
            toast.error('An error occurred while renaming the view.');
        } finally {
            saving = false;
        }
    }

    async function handleDelete() {
        if (!deleteTarget || !organizationId) {
            return;
        }

        const target = deleteTarget;
        try {
            const response = await client.delete(`saved-views/${target.id}`, { expectedStatusCodes: [202] });
            if (response.ok) {
                if (activeSavedView?.id === target.id) {
                    onClearSavedView();
                }
                toast.success(`View "${target.name}" deleted.`);

                // Optimistically remove from all caches immediately (ES has ~1s refresh delay)
                queryClient.setQueryData(queryKeys.view(organizationId, target.view), (old: SavedView[] | undefined) => old?.filter((v) => v.id !== target.id));
                queryClient.setQueryData(queryKeys.organization(organizationId), (old: SavedView[] | undefined) => old?.filter((v) => v.id !== target.id));
                // Delay invalidation to allow Elasticsearch (~1s refresh) to index the deletion
                setTimeout(() => {
                    void queryClient.invalidateQueries({ queryKey: queryKeys.type });
                }, 1500);
            } else {
                toast.error('Failed to delete view. Please try again.');
            }
        } catch {
            toast.error('An error occurred while deleting the view.');
        } finally {
            deleteDialogOpen = false;
            deleteTarget = null;
        }
    }

    function handleSelect(savedView: SavedView) {
        onLoadView(savedView.id);
    }

    async function handleToggleDefault() {
        if (!effectiveView || !organizationId) {
            return;
        }

        saving = true;
        const viewId = effectiveView.id;
        const viewName = effectiveView.view;
        try {
            const body: UpdateSavedView = { is_default: true };
            const response = await client.patchJSON(`saved-views/${effectiveView.id}`, body, { expectedStatusCodes: [422] });
            if (response.ok) {
                toast.success('Set as default.');

                // Optimistically update the is_default flag in all caches immediately
                const updateViews = (old: SavedView[] | undefined): SavedView[] | undefined => {
                    if (!old) {
                        return old;
                    }
                    return old.map((v) => {
                        // Clear the old org-wide default for this view
                        if (v.id !== viewId && v.view === viewName && !v.user_id) {
                            return { ...v, is_default: false };
                        }
                        if (v.id === viewId) {
                            return { ...v, is_default: true };
                        }
                        return v;
                    });
                };

                queryClient.setQueryData(queryKeys.view(organizationId, viewName), updateViews);
                queryClient.setQueryData(queryKeys.organization(organizationId), updateViews);
                // Delay invalidation to allow Elasticsearch (~1s refresh) to index the change
                // so the background refetch doesn't overwrite the optimistic update
                setTimeout(() => {
                    void queryClient.invalidateQueries({ queryKey: queryKeys.type });
                }, 1500);
            } else {
                const message = response.problem?.title ?? 'Failed to update default setting.';
                toast.error(message);
            }
        } catch {
            toast.error('An error occurred while updating the default setting.');
        } finally {
            saving = false;
        }
    }
</script>

<DropdownMenu.Root>
    <DropdownMenu.Trigger>
        {#snippet child({ props })}
            <Button {...props} class="gap-x-1.5 px-3" size="lg" variant="outline">
                <Save class="size-4" />
                {#if effectiveView}
                    <span class="max-w-37.5 truncate">{effectiveView.name}</span>
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
                <DropdownMenu.Item disabled={saving} onclick={handleUpdate}>
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
                                {#if effectiveView?.id === savedView.id}
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
                                        <Tooltip.Content class="max-w-xs">
                                            {#if savedView.filter}
                                                <p class="font-mono text-xs">{savedView.filter}</p>
                                            {/if}
                                            {#if savedView.time}
                                                <p class="text-muted-foreground text-xs">{timeLabels.get(savedView.time) ?? savedView.time}</p>
                                            {/if}
                                        </Tooltip.Content>
                                    </Tooltip.Root>
                                {:else}
                                    <span class="text-muted-foreground text-left text-[11px]">No filters</span>
                                {/if}
                            </span>
                        </span>
                        <button
                            class="text-muted-foreground hover:text-destructive -mr-1 flex size-5 shrink-0 items-center justify-center rounded-sm"
                            aria-label="Delete {savedView.name}"
                            title="Delete {savedView.name}"
                            onclick={(e) => {
                                e.stopPropagation();
                                openDeleteDialog(savedView);
                            }}
                        >
                            <Trash2 class="size-3" aria-hidden="true" />
                        </button>
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
            {#if !effectiveView}
                <DropdownMenu.Item onclick={openSaveDialog}>
                    <Plus class="mr-2 size-4" />
                    Save current view
                </DropdownMenu.Item>
            {/if}
            {#if effectiveView}
                <DropdownMenu.Item onclick={openRenameDialog}>
                    <Pencil class="mr-2 size-4" />
                    Rename "{effectiveView.name}"
                </DropdownMenu.Item>
                {#if !effectiveView.user_id && !effectiveView.is_default}
                    <DropdownMenu.Item disabled={saving} onclick={handleToggleDefault}>
                        <Star class="mr-2 size-4" />
                        Set as default for everyone
                    </DropdownMenu.Item>
                {/if}
                <DropdownMenu.Separator />
                <DropdownMenu.Item class="text-destructive" onclick={() => openDeleteDialog(effectiveView)}>
                    <Trash2 class="mr-2 size-4" />
                    Delete "{effectiveView.name}"
                </DropdownMenu.Item>
                {#if !isModified}
                    <DropdownMenu.Separator />
                    <DropdownMenu.Item onclick={onClearSavedView}>Clear Saved View</DropdownMenu.Item>
                {/if}
            {/if}
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

<!-- Save Dialog -->
{#if saveDialogOpen}
    <Dialog.Root bind:open={saveDialogOpen}>
        <Dialog.Content class="sm:max-w-100">
            <Dialog.Header>
                <Dialog.Title>Save View</Dialog.Title>
                <Dialog.Description>Save the current view configuration for quick access.</Dialog.Description>
            </Dialog.Header>
            {#if duplicateView}
                <div class="bg-muted rounded-md p-3">
                    <p class="text-sm">
                        Current filters match <strong>"{duplicateView.name}"</strong>. You can
                        <button
                            class="text-primary underline"
                            onclick={() => {
                                saveDialogOpen = false;
                                handleSelect(duplicateView);
                            }}>load it</button
                        > instead, or save with a different name.
                    </p>
                </div>
            {/if}
            <form
                class="flex flex-col gap-4"
                onsubmit={(e) => {
                    e.preventDefault();
                    handleSave();
                }}
            >
                <div class="flex flex-col gap-2">
                    <Label for="filter-name">Name</Label>
                    <Input id="filter-name" bind:value={saveName} placeholder="e.g., Production Errors" required />
                </div>
                <div class="flex items-center justify-between">
                    <div>
                        <Label for="filter-private" class="text-sm">Private</Label>
                        <p class="text-muted-foreground text-xs">Only visible to you</p>
                    </div>
                    <Switch
                        id="filter-private"
                        bind:checked={savePrivate}
                        onCheckedChange={(checked) => {
                            if (checked) {
                                saveAsDefault = false;
                            }
                        }}
                    />
                </div>
                {#if !savePrivate}
                    <div class="flex items-center justify-between">
                        <div>
                            <Label for="filter-default" class="text-sm">Set as default</Label>
                            <p class="text-muted-foreground text-xs">Auto-loads for everyone on page visit</p>
                        </div>
                        <Switch id="filter-default" bind:checked={saveAsDefault} />
                    </div>
                {/if}
                <Dialog.Footer>
                    <Button variant="outline" onclick={() => (saveDialogOpen = false)}>Cancel</Button>
                    <Button type="submit" disabled={!saveName.trim() || saving}>
                        {saving ? 'Saving...' : 'Save'}
                    </Button>
                </Dialog.Footer>
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}

<!-- Rename Dialog -->
{#if renameDialogOpen}
    <Dialog.Root bind:open={renameDialogOpen}>
        <Dialog.Content class="sm:max-w-100">
            <Dialog.Header>
                <Dialog.Title>Rename View</Dialog.Title>
                <Dialog.Description>Change the display name for this saved view.</Dialog.Description>
            </Dialog.Header>
            <form
                class="flex flex-col gap-4"
                onsubmit={(e) => {
                    e.preventDefault();
                    handleRename();
                }}
            >
                <div class="flex flex-col gap-2">
                    <Label for="rename-filter">Name</Label>
                    <Input id="rename-filter" bind:value={renameName} placeholder="View name" required />
                </div>
                <Dialog.Footer>
                    <Button variant="outline" onclick={() => (renameDialogOpen = false)}>Cancel</Button>
                    <Button type="submit" disabled={!renameName.trim() || saving}>
                        {saving ? 'Saving...' : 'Rename'}
                    </Button>
                </Dialog.Footer>
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}

<!-- Delete Confirmation -->
{#if deleteDialogOpen && deleteTarget}
    <AlertDialog.Root bind:open={deleteDialogOpen}>
        <AlertDialog.Content>
            <AlertDialog.Header>
                <AlertDialog.Title>Delete Saved View</AlertDialog.Title>
                <AlertDialog.Description>
                    Are you sure you want to delete "{deleteTarget.name}"? This action cannot be undone.
                </AlertDialog.Description>
            </AlertDialog.Header>
            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action onclick={handleDelete}>Delete</AlertDialog.Action>
            </AlertDialog.Footer>
        </AlertDialog.Content>
    </AlertDialog.Root>
{/if}
