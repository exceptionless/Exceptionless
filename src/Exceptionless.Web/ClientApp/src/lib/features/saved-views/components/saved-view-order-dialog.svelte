<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import ChevronDown from '@lucide/svelte/icons/chevron-down';
    import ChevronUp from '@lucide/svelte/icons/chevron-up';
    import GripVertical from '@lucide/svelte/icons/grip-vertical';
    import LoaderCircle from '@lucide/svelte/icons/loader-circle';
    import RotateCcw from '@lucide/svelte/icons/rotate-ccw';
    import { toast } from 'svelte-sonner';

    import type { SavedView } from '../models';

    type SavedViewOrderItem = Pick<SavedView, 'id' | 'name' | 'user_id'>;

    interface Props {
        onSave: (savedViewIds: string[]) => Promise<void>;
        open: boolean;
        savedViews: SavedViewOrderItem[];
        title: string;
    }

    let { onSave, open = $bindable(), savedViews, title }: Props = $props();
    let draggedSavedViewId = $state<null | string>(null);
    let orderedSavedViews = $state<SavedViewOrderItem[]>([]);
    let saving = $state(false);
    let wasOpen = $state(false);

    $effect(() => {
        if (open && !wasOpen) {
            orderedSavedViews = [...savedViews];
        }

        wasOpen = open;
    });

    function applyOrder(savedViewIds: string[]): void {
        const byId = new Map(orderedSavedViews.map((savedView) => [savedView.id, savedView]));
        orderedSavedViews = savedViewIds.map((id) => byId.get(id)).filter((savedView): savedView is SavedViewOrderItem => !!savedView);
    }

    function move(savedViewId: string, offset: -1 | 1): void {
        const savedViewIds = orderedSavedViews.map((savedView) => savedView.id);
        const currentIndex = savedViewIds.indexOf(savedViewId);
        const targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= savedViewIds.length) {
            return;
        }

        [savedViewIds[currentIndex], savedViewIds[targetIndex]] = [savedViewIds[targetIndex]!, savedViewIds[currentIndex]!];
        applyOrder(savedViewIds);
    }

    function handleDragStart(event: DragEvent, savedViewId: string): void {
        draggedSavedViewId = savedViewId;
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', savedViewId);
        }
    }

    function handleDragOver(event: DragEvent, targetSavedViewId: string): void {
        event.preventDefault();
        if (event.dataTransfer) {
            event.dataTransfer.dropEffect = 'move';
        }

        if (!draggedSavedViewId || draggedSavedViewId === targetSavedViewId) {
            return;
        }

        const savedViewIds = orderedSavedViews.map((savedView) => savedView.id);
        const fromIndex = savedViewIds.indexOf(draggedSavedViewId);
        const toIndex = savedViewIds.indexOf(targetSavedViewId);
        if (fromIndex < 0 || toIndex < 0) {
            return;
        }

        const [movedSavedViewId] = savedViewIds.splice(fromIndex, 1);
        if (movedSavedViewId) {
            savedViewIds.splice(toIndex, 0, movedSavedViewId);
            applyOrder(savedViewIds);
        }
    }

    async function save(savedViewIds: string[], successMessage: string): Promise<void> {
        saving = true;
        try {
            await onSave(savedViewIds);
            toast.success(successMessage);
            open = false;
        } catch {
            toast.error(`Failed to update your ${title.toLowerCase()} view order. Please try again.`);
        } finally {
            saving = false;
        }
    }

    function saveOrder(): void {
        void save(
            orderedSavedViews.map((savedView) => savedView.id),
            `${title} view order saved.`
        );
    }

    function resetOrder(): void {
        void save([], `${title} views reset to alphabetical order.`);
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="max-h-[calc(100dvh-2rem)] gap-0 overflow-hidden p-0 sm:max-w-xl" preventScroll={false}>
        <Dialog.Header class="border-b px-6 py-5 pr-14">
            <Dialog.Title>Reorder {title} Views</Dialog.Title>
            <Dialog.Description>This order is personal to you. Drag views or use the move buttons to arrange them.</Dialog.Description>
        </Dialog.Header>

        <div class="min-h-0 px-6 py-5">
            <div class="border-input bg-muted/20 rounded-lg border p-2">
                <div class="flex max-h-[24rem] flex-col gap-1.5 overflow-y-auto pr-1" role="list" aria-label={`${title} saved views`}>
                    {#each orderedSavedViews as savedView, index (savedView.id)}
                        <div
                            class="bg-background hover:bg-muted/70 flex min-h-11 items-center gap-3 rounded-lg border px-3 text-sm shadow-xs transition-colors"
                            draggable="true"
                            ondragstart={(event) => handleDragStart(event, savedView.id)}
                            ondragover={(event) => handleDragOver(event, savedView.id)}
                            ondragend={() => (draggedSavedViewId = null)}
                            role="listitem"
                        >
                            <GripVertical class="text-muted-foreground/70 cursor-grab" aria-hidden="true" />
                            <span class="min-w-0 flex-1 truncate font-medium">{savedView.name}</span>
                            <Badge variant={savedView.user_id ? 'secondary' : 'outline'}>{savedView.user_id ? 'Private' : 'Shared'}</Badge>
                            <div class="flex shrink-0 items-center gap-1">
                                <Button
                                    variant="ghost"
                                    size="icon-sm"
                                    onclick={() => move(savedView.id, -1)}
                                    disabled={saving || index === 0}
                                    title={`Move ${savedView.name} up`}
                                >
                                    <ChevronUp />
                                    <span class="sr-only">Move {savedView.name} up</span>
                                </Button>
                                <Button
                                    variant="ghost"
                                    size="icon-sm"
                                    onclick={() => move(savedView.id, 1)}
                                    disabled={saving || index === orderedSavedViews.length - 1}
                                    title={`Move ${savedView.name} down`}
                                >
                                    <ChevronDown />
                                    <span class="sr-only">Move {savedView.name} down</span>
                                </Button>
                            </div>
                        </div>
                    {/each}
                </div>
            </div>
        </div>

        <Dialog.Footer class="mx-0 mb-0 rounded-b-xl px-6 py-4">
            <Button variant="outline" onclick={resetOrder} disabled={saving}>
                <RotateCcw data-icon="inline-start" />
                Reset to alphabetical
            </Button>
            <Dialog.Close>
                {#snippet child({ props })}
                    <Button variant="outline" disabled={saving} {...props}>Cancel</Button>
                {/snippet}
            </Dialog.Close>
            <Button onclick={saveOrder} disabled={saving}>
                {#if saving}
                    <LoaderCircle data-icon="inline-start" class="animate-spin" />
                {/if}
                Save order
            </Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
