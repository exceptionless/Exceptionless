<script lang="ts">
    import type { ProductTourCheckpointName } from '$features/product-tours/types';

    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import { Switch } from '$comp/ui/switch';
    import ProductTourInlineCallout from '$features/product-tours/components/product-tour-inline-callout.svelte';

    import type { SavedView } from '../models';

    import {
        findSavedViewByName,
        findSavedViewBySlug,
        isSavedViewSlugReserved,
        isSavedViewSlugValid,
        SAVED_VIEW_NAME_MAX_LENGTH,
        SAVED_VIEW_SLUG_MAX_LENGTH,
        savedViewSlug
    } from '../slugs';

    interface Props {
        defaultPrivate?: boolean;
        duplicateView?: SavedView;
        onCancel?: () => void;
        onClose: () => void;
        onLoadView: (view: SavedView) => Promise<void> | void;
        onSave: (name: string, slug: string, isPrivate: boolean) => Promise<void>;
        onTourContinue?: (checkpointName: ProductTourCheckpointName) => void;
        open: boolean;
        pendingCompletion?: boolean;
        savedViews: SavedView[];
        saving: boolean;
        tourCheckpointName?: ProductTourCheckpointName;
    }

    let {
        defaultPrivate = false,
        duplicateView,
        onCancel,
        onClose,
        onLoadView,
        onSave,
        onTourContinue,
        open = $bindable(),
        pendingCompletion = false,
        savedViews,
        saving,
        tourCheckpointName
    }: Props = $props();

    let saveName = $state('');
    let saveSlug = $state('');
    let isSlugDirty = $state(false);
    let isPrivate = $state(false);
    let attemptedSubmit = $state(false);

    const trimmedName = $derived(saveName.trim());
    const normalizedSlug = $derived(savedViewSlug(saveSlug));
    const duplicateName = $derived(findSavedViewByName(savedViews, trimmedName));
    const duplicateSlug = $derived(findSavedViewBySlug(savedViews, normalizedSlug));
    const nameError = $derived.by(() => {
        if (!trimmedName) {
            return 'Name is required.';
        }

        if (trimmedName.length > SAVED_VIEW_NAME_MAX_LENGTH) {
            return `Name cannot exceed ${SAVED_VIEW_NAME_MAX_LENGTH} characters.`;
        }

        if (duplicateName) {
            return `A saved view named "${duplicateName.name}" already exists.`;
        }

        return undefined;
    });
    const slugError = $derived.by(() => {
        if (!normalizedSlug) {
            return 'URL name is required. Use at least one letter or number.';
        }

        if (normalizedSlug.length > SAVED_VIEW_SLUG_MAX_LENGTH) {
            return `URL name cannot exceed ${SAVED_VIEW_SLUG_MAX_LENGTH} characters.`;
        }

        if (!isSavedViewSlugValid(normalizedSlug)) {
            if (isSavedViewSlugReserved(normalizedSlug)) {
                return 'URL name cannot look like an event or stack id.';
            }

            return 'URL name can only contain lowercase letters, numbers, and single dashes.';
        }

        if (duplicateSlug) {
            return `A saved view with the URL name "${normalizedSlug}" already exists.`;
        }

        return undefined;
    });
    const visibleNameError = $derived(attemptedSubmit || saveName.length > 0 ? nameError : undefined);
    const visibleSlugError = $derived(attemptedSubmit || saveName.length > 0 || saveSlug.length > 0 ? slugError : undefined);
    const canSave = $derived((pendingCompletion || (!nameError && !slugError)) && !saving);

    $effect(() => {
        if (open) {
            saveName = '';
            saveSlug = '';
            isSlugDirty = false;
            isPrivate = defaultPrivate;
            attemptedSubmit = false;
        }
    });

    $effect(() => {
        if (!isSlugDirty) {
            saveSlug = savedViewSlug(saveName);
        }
    });

    $effect(() => {
        const normalizedSlug = savedViewSlug(saveSlug);
        if (saveSlug !== normalizedSlug) {
            saveSlug = normalizedSlug;
        }
    });

    async function handleSave() {
        attemptedSubmit = true;
        if (saving || (!pendingCompletion && (nameError || slugError))) {
            return;
        }

        await onSave(trimmedName, normalizedSlug, isPrivate);
    }

    function dismissTour(): void {
        onClose();
        onCancel?.();
    }
</script>

<Dialog.Root
    {open}
    onOpenChange={(nextOpen) => {
        if (nextOpen || !saving) {
            open = nextOpen;
            if (!nextOpen) {
                onCancel?.();
            }
        }
    }}
>
    <Dialog.Content
        class="sm:max-w-100"
        data-tour="saved-view-dialog"
        onEscapeKeydown={(event) => saving && event.preventDefault()}
        onInteractOutside={(event) => saving && event.preventDefault()}
    >
        <Dialog.Header>
            <Dialog.Title>Save View</Dialog.Title>
            <Dialog.Description>Save the current view configuration for quick access.</Dialog.Description>
        </Dialog.Header>
        {#if defaultPrivate && tourCheckpointName === 'name-view'}
            <ProductTourInlineCallout
                description="Review the current filters, time, display options, and columns. Choose a meaningful name, then continue."
                onContinue={() => onTourContinue?.('private-view')}
                onDismiss={dismissTour}
                title="Review and name your view"
                tourName="create-saved-view"
            />
        {:else if defaultPrivate && tourCheckpointName === 'private-view'}
            <ProductTourInlineCallout
                description="Private is enabled for this guide so the practice view is visible only to you. Continue when you are ready to save it."
                onContinue={() => onTourContinue?.('save-view')}
                onDismiss={dismissTour}
                title="Keep it private"
                tourName="create-saved-view"
            />
        {:else if defaultPrivate && (tourCheckpointName === 'save-view' || tourCheckpointName === 'view-created')}
            <ProductTourInlineCallout
                description={pendingCompletion
                    ? 'The view was created, but the guide could not save its progress. Retry to finish without creating another view.'
                    : 'Click Save when ready. The guide completes only after the private view is successfully created and loaded.'}
                onDismiss={dismissTour}
                title="Create the saved view"
                tourName="create-saved-view"
            />
        {/if}
        {#if duplicateView && !pendingCompletion}
            <div class="bg-muted rounded-md p-3">
                <Muted>
                    Current filters match <strong>"{duplicateView.name}"</strong>. You can
                    <Button
                        variant="link"
                        class="h-auto p-0 text-sm"
                        onclick={async () => {
                            await onLoadView(duplicateView);
                            dismissTour();
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
                <Label for="view-name">Name</Label>
                <Input
                    data-tour="saved-view-name"
                    id="view-name"
                    bind:value={saveName}
                    placeholder="e.g., Production Errors"
                    maxlength={SAVED_VIEW_NAME_MAX_LENGTH}
                    aria-invalid={!!visibleNameError}
                    aria-describedby={visibleNameError ? 'view-name-error' : undefined}
                    required
                    autofocus
                    disabled={pendingCompletion}
                />
                {#if visibleNameError}
                    <p id="view-name-error" class="text-destructive text-sm">{visibleNameError}</p>
                {/if}
            </div>
            <div class="flex flex-col gap-2">
                <Label for="view-slug">URL name</Label>
                <Input
                    id="view-slug"
                    bind:value={saveSlug}
                    placeholder="production-errors"
                    maxlength={SAVED_VIEW_SLUG_MAX_LENGTH}
                    aria-invalid={!!visibleSlugError}
                    aria-describedby={visibleSlugError ? 'view-slug-error' : undefined}
                    required
                    disabled={pendingCompletion}
                    oninput={() => {
                        isSlugDirty = true;
                    }}
                />
                {#if visibleSlugError}
                    <p id="view-slug-error" class="text-destructive text-sm">{visibleSlugError}</p>
                {/if}
            </div>
            <div class="flex items-center justify-between" data-tour="saved-view-private">
                <div>
                    <Label for="view-private" class="text-sm">Private</Label>
                    <Muted>{defaultPrivate ? 'Required for this guided practice view' : 'Only visible to you'}</Muted>
                </div>
                <Switch disabled={defaultPrivate || pendingCompletion} id="view-private" bind:checked={isPrivate} />
            </div>
            <Dialog.Footer>
                <Button variant="outline" disabled={saving} onclick={dismissTour}>Cancel</Button>
                <Button data-tour="saved-view-submit" type="submit" disabled={!canSave}>
                    {saving ? 'Saving...' : pendingCompletion ? 'Retry guide completion' : 'Save'}
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
