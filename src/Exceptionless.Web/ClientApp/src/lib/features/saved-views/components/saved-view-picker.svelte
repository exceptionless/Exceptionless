<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';

    import { Muted } from '$comp/typography';
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
    import Check from '@lucide/svelte/icons/check';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import Pencil from '@lucide/svelte/icons/pencil';
    import Plus from '@lucide/svelte/icons/plus';
    import Save from '@lucide/svelte/icons/save';
    import Star from '@lucide/svelte/icons/star';
    import Trash2 from '@lucide/svelte/icons/trash-2';
    import Undo2 from '@lucide/svelte/icons/undo-2';
    import { tick, untrack } from 'svelte';
    import { toast } from 'svelte-sonner';
    import { SvelteMap } from 'svelte/reactivity';

    import type { NewSavedView, SavedView, UpdateSavedView } from '../models';

    import { deleteSavedView, patchSavedView, postSavedView } from '../api.svelte';

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

    let isSaveDialogOpen = $state(false);
    let isRenameDialogOpen = $state(false);
    let isDeleteDialogOpen = $state(false);
    let viewToDelete = $state<null | SavedView>(null);
    let saveName = $state('');
    let isPrivate = $state(false);
    let isDefault = $state(false);
    let renameName = $state('');

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

    // Auto-detect if current filters match an existing saved view (even without ?saved=)
    const matchingSavedView = $derived.by(() => {
        if (activeSavedView) return undefined;
        if (!currentFilterString) return undefined;

        return savedViews.find((savedView) => {
            if (savedView.filter !== currentFilterString) return false;
            if (savedView.time && (time ?? '') !== savedView.time) return false;
            return true;
        });
    });

    // The currently active view: either explicitly loaded via ?saved= or auto-matched by filter content
    const activeView = $derived(activeSavedView ?? matchingSavedView);

    // When filters match an existing view without an explicit ?saved= param,
    // update the URL so the sidebar highlights the correct view.
    $effect(() => {
        const matched = matchingSavedView;
        if (matched) {
            untrack(() => onLoadView(matched.id));
        }
    });

    const duplicateView = $derived.by(() => {
        return savedViews.find((savedView) => {
            if (savedView.filter !== currentFilterString) return false;
            if (savedView.time && (time ?? '') !== savedView.time) return false;
            return true;
        });
    });

    async function openSaveDialog() {
        saveName = '';
        isPrivate = false;
        isDefault = false;
        await tick();
        isSaveDialogOpen = true;
    }

    async function openRenameDialog() {
        if (!activeView) {
            return;
        }

        renameName = activeView.name;
        await tick();
        isRenameDialogOpen = true;
    }

    async function openDeleteDialog(savedView: SavedView) {
        viewToDelete = savedView;
        await tick();
        isDeleteDialogOpen = true;
    }

    async function handleSave() {
        if (!organizationId || !saveName.trim()) {
            return;
        }

        const filterDefinitions = serializeFilters(filters);
        const body: NewSavedView & { is_private?: boolean } = {
            columns: columnVisibility,
            filter: currentFilterString || undefined,
            filter_definitions: filterDefinitions,
            is_default: isDefault || undefined,
            is_private: isPrivate || undefined,
            name: saveName.trim(),
            organization_id: organizationId,
            time: time || undefined,
            view
        };

        try {
            const result = await createMutation.mutateAsync(body);
            isSaveDialogOpen = false;
            onLoadView(result.id);
            toast.success(`Saved view "${result.name}" created.`);
        } catch (error) {
            const problem = (error as { title?: string })?.title;
            toast.error(problem ?? 'Failed to save view. Please try again.');
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
            const problem = (error as { title?: string })?.title;
            toast.error(problem ?? 'Failed to update view. Please try again.');
        }
    }

    async function handleRename() {
        if (!activeView || !organizationId || !renameName.trim()) {
            return;
        }

        try {
            await updateMutation.mutateAsync({ name: renameName.trim() });
            isRenameDialogOpen = false;
            toast.success('View renamed.');
        } catch (error) {
            const problem = (error as { title?: string })?.title;
            toast.error(problem ?? 'Failed to rename view. Please try again.');
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
            const problem = (error as { title?: string })?.title;
            toast.error(problem ?? 'Failed to update default setting.');
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
                <DropdownMenu.Separator />
                <DropdownMenu.Item class="text-destructive" onclick={() => openDeleteDialog(activeView)}>
                    <Trash2 class="mr-2 size-4" />
                    Delete "{activeView.name}"
                </DropdownMenu.Item>
                {#if !isModified}
                    <DropdownMenu.Separator />
                    <DropdownMenu.Item onclick={onClearSavedView}>Clear Saved View</DropdownMenu.Item>
                {/if}
            {/if}
        </DropdownMenu.Group>
    </DropdownMenu.Content>
</DropdownMenu.Root>

{#if isSaveDialogOpen}
    <Dialog.Root bind:open={isSaveDialogOpen}>
        <Dialog.Content class="sm:max-w-100">
            <Dialog.Header>
                <Dialog.Title>Save View</Dialog.Title>
                <Dialog.Description>Save the current view configuration for quick access.</Dialog.Description>
            </Dialog.Header>
            {#if duplicateView}
                <div class="bg-muted rounded-md p-3">
                    <Muted>
                        Current filters match <strong>"{duplicateView.name}"</strong>. You can
                        <Button
                            variant="link"
                            class="h-auto p-0 text-sm"
                            onclick={() => {
                                isSaveDialogOpen = false;
                                handleSelect(duplicateView);
                            }}>load it</Button
                        > instead, or save with a different name.
                    </Muted>
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
                        <Muted>Only visible to you</Muted>
                    </div>
                    <Switch
                        id="filter-private"
                        bind:checked={isPrivate}
                        onCheckedChange={(checked) => {
                            if (checked) {
                                isDefault = false;
                            }
                        }}
                    />
                </div>
                {#if !isPrivate}
                    <div class="flex items-center justify-between">
                        <div>
                            <Label for="filter-default" class="text-sm">Set as default</Label>
                            <Muted>Auto-loads for everyone on page visit</Muted>
                        </div>
                        <Switch id="filter-default" bind:checked={isDefault} />
                    </div>
                {/if}
                <Dialog.Footer>
                    <Button variant="outline" onclick={() => (isSaveDialogOpen = false)}>Cancel</Button>
                    <Button type="submit" disabled={!saveName.trim() || saving}>
                        {saving ? 'Saving...' : 'Save'}
                    </Button>
                </Dialog.Footer>
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}

{#if isRenameDialogOpen}
    <Dialog.Root bind:open={isRenameDialogOpen}>
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
                    <Button variant="outline" onclick={() => (isRenameDialogOpen = false)}>Cancel</Button>
                    <Button type="submit" disabled={!renameName.trim() || saving}>
                        {saving ? 'Saving...' : 'Rename'}
                    </Button>
                </Dialog.Footer>
            </form>
        </Dialog.Content>
    </Dialog.Root>
{/if}

{#if isDeleteDialogOpen && viewToDelete}
    <AlertDialog.Root bind:open={isDeleteDialogOpen}>
        <AlertDialog.Content>
            <AlertDialog.Header>
                <AlertDialog.Title>Delete Saved View</AlertDialog.Title>
                <AlertDialog.Description>
                    Are you sure you want to delete "{viewToDelete.name}"? This action cannot be undone.
                </AlertDialog.Description>
            </AlertDialog.Header>
            <AlertDialog.Footer>
                <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
                <AlertDialog.Action onclick={handleDelete}>Delete</AlertDialog.Action>
            </AlertDialog.Footer>
        </AlertDialog.Content>
    </AlertDialog.Root>
{/if}
