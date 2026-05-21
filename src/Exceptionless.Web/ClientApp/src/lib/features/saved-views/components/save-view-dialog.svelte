<script lang="ts">
    import { Muted } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';
    import { Switch } from '$comp/ui/switch';

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
        duplicateView?: SavedView;
        onClose: () => void;
        onLoadView: (view: SavedView) => void;
        onSave: (name: string, slug: string, isPrivate: boolean) => Promise<void>;
        open: boolean;
        savedViews: SavedView[];
        saving: boolean;
    }

    let { duplicateView, onClose, onLoadView, onSave, open = $bindable(), savedViews, saving }: Props = $props();

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
                return 'URL name cannot look like an event or issue id.';
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
    const canSave = $derived(!nameError && !slugError && !saving);

    $effect(() => {
        if (open) {
            saveName = '';
            saveSlug = '';
            isSlugDirty = false;
            isPrivate = false;
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
        if (!canSave) {
            return;
        }

        await onSave(trimmedName, normalizedSlug, isPrivate);
    }
</script>

<Dialog.Root bind:open>
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
                            open = false;
                            onLoadView(duplicateView);
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
                    id="view-name"
                    bind:value={saveName}
                    placeholder="e.g., Production Errors"
                    maxlength={SAVED_VIEW_NAME_MAX_LENGTH}
                    aria-invalid={!!visibleNameError}
                    aria-describedby={visibleNameError ? 'view-name-error' : undefined}
                    required
                    autofocus
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
                    oninput={() => {
                        isSlugDirty = true;
                    }}
                />
                {#if visibleSlugError}
                    <p id="view-slug-error" class="text-destructive text-sm">{visibleSlugError}</p>
                {/if}
            </div>
            <div class="flex items-center justify-between">
                <div>
                    <Label for="view-private" class="text-sm">Private</Label>
                    <Muted>Only visible to you</Muted>
                </div>
                <Switch id="view-private" bind:checked={isPrivate} />
            </div>
            <Dialog.Footer>
                <Button variant="outline" onclick={onClose}>Cancel</Button>
                <Button type="submit" disabled={!canSave}>
                    {saving ? 'Saving...' : 'Save'}
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
