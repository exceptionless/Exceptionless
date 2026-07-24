<script lang="ts">
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Label } from '$comp/ui/label';

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
        name: string;
        onClose: () => void;
        onRename: (newName: string, slug: string) => Promise<void>;
        open: boolean;
        savedViews: SavedView[];
        saving: boolean;
        slug?: null | string;
        viewId: string;
    }

    let { name, onClose, onRename, open = $bindable(), savedViews, saving, slug, viewId }: Props = $props();

    let renameName = $state('');
    let renameSlug = $state('');
    let isSlugDirty = $state(false);
    let attemptedSubmit = $state(false);

    const trimmedName = $derived(renameName.trim());
    const normalizedSlug = $derived(savedViewSlug(renameSlug));
    const duplicateName = $derived(findSavedViewByName(savedViews, trimmedName, viewId));
    const duplicateSlug = $derived(findSavedViewBySlug(savedViews, normalizedSlug, viewId));
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
    const visibleNameError = $derived(attemptedSubmit || renameName.length > 0 ? nameError : undefined);
    const visibleSlugError = $derived(attemptedSubmit || renameName.length > 0 || renameSlug.length > 0 ? slugError : undefined);
    const canRename = $derived(!nameError && !slugError && !saving);

    $effect(() => {
        if (open) {
            renameName = name;
            renameSlug = savedViewSlug(slug || name);
            isSlugDirty = false;
            attemptedSubmit = false;
        }
    });

    $effect(() => {
        if (open && !isSlugDirty) {
            renameSlug = savedViewSlug(renameName);
        }
    });

    $effect(() => {
        const normalizedSlug = savedViewSlug(renameSlug);
        if (renameSlug !== normalizedSlug) {
            renameSlug = normalizedSlug;
        }
    });

    async function handleRename() {
        attemptedSubmit = true;
        if (!canRename) {
            return;
        }

        await onRename(trimmedName, normalizedSlug);
    }
</script>

<Dialog.Root bind:open>
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
                <Label for="rename-view">Name</Label>
                <Input
                    id="rename-view"
                    bind:value={renameName}
                    placeholder="View name"
                    maxlength={SAVED_VIEW_NAME_MAX_LENGTH}
                    aria-invalid={!!visibleNameError}
                    aria-describedby={visibleNameError ? 'rename-view-error' : undefined}
                    required
                    autofocus
                />
                {#if visibleNameError}
                    <p id="rename-view-error" class="text-sm text-destructive">{visibleNameError}</p>
                {/if}
            </div>
            <div class="flex flex-col gap-2">
                <Label for="rename-view-slug">URL name</Label>
                <Input
                    id="rename-view-slug"
                    bind:value={renameSlug}
                    placeholder="view-slug"
                    maxlength={SAVED_VIEW_SLUG_MAX_LENGTH}
                    aria-invalid={!!visibleSlugError}
                    aria-describedby={visibleSlugError ? 'rename-view-slug-error' : undefined}
                    required
                    oninput={() => {
                        isSlugDirty = true;
                    }}
                />
                {#if visibleSlugError}
                    <p id="rename-view-slug-error" class="text-sm text-destructive">{visibleSlugError}</p>
                {/if}
            </div>
            <Dialog.Footer>
                <Button variant="outline" onclick={onClose}>Cancel</Button>
                <Button type="submit" disabled={!canRename}>
                    {saving ? 'Saving...' : 'Rename'}
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
