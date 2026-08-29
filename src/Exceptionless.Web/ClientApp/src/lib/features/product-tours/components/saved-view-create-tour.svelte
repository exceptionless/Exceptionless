<script lang="ts">
    import type { SavedView } from '$features/saved-views/models';

    import { toast } from 'svelte-sonner';

    import type { ProductTourCheckpoint } from '../types';

    import { createProductTourActions } from '../actions.svelte';
    import { productTourCheckpoint } from '../state.svelte';
    import ProductTourSpotlight from './product-tour-spotlight.svelte';

    interface Props {
        closeMenu: () => void;
        onLoadView: (view: SavedView) => Promise<void> | void;
        openMenu: () => void;
        openSaveDialog: () => void;
        savedViews: SavedView[];
    }

    let { closeMenu, onLoadView, openMenu, openSaveDialog, savedViews }: Props = $props();
    const actions = createProductTourActions();
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'saved-view-create' ? productTourCheckpoint.current : undefined);
    const pendingView = $derived.by(() => {
        const phase = checkpoint?.phase;
        return phase?.type === 'saved-view-created' || phase?.type === 'saved-view-loaded'
            ? savedViews.find((savedView) => savedView.id === phase.viewId)
            : undefined;
    });

    export function validateSave(isPrivate: boolean): boolean {
        if (!checkpoint) {
            return true;
        }

        if (checkpoint.checkpointName !== 'save-view') {
            toast.error('Continue the guide before creating the practice view.');
            return false;
        }

        if (isPrivate) {
            return true;
        }

        toast.error('Enable Private before creating the guided practice view.');
        return false;
    }

    export function shouldDefaultPrivate(): boolean {
        return Boolean(checkpoint);
    }

    export async function created(view: SavedView): Promise<void> {
        const active = checkpoint;
        if (!active) {
            await onLoadView(view);
            return;
        }

        const createdCheckpoint = productTourCheckpoint.advance(active, 'view-created', {
            type: 'saved-view-created',
            viewId: view.id
        });
        if (createdCheckpoint) {
            await loadAndComplete(createdCheckpoint, view);
        }
    }

    export async function closed(): Promise<void> {
        const active = checkpoint;
        if (active?.phase.type === 'active' && ['name-view', 'private-view', 'save-view'].includes(active.checkpointName)) {
            await actions.dismiss(active);
        }
    }

    async function retry(): Promise<void> {
        const active = checkpoint;
        if (!active) {
            return;
        }

        if (active.phase.type === 'saved-view-loaded') {
            await actions.complete(active);
            return;
        }

        if (active.phase.type === 'saved-view-created') {
            if (!pendingView) {
                toast.error('The created view could not be loaded. Refresh and try again.');
                return;
            }

            await loadAndComplete(active, pendingView);
        }
    }

    async function loadAndComplete(active: ProductTourCheckpoint<'saved-view-create'>, view: SavedView): Promise<void> {
        try {
            await onLoadView(view);
            const loadedCheckpoint = productTourCheckpoint.advance(active, 'view-created', {
                type: 'saved-view-loaded',
                viewId: view.id
            });
            if (loadedCheckpoint) {
                await actions.complete(loadedCheckpoint);
            }
        } catch {
            toast.error('Failed to load the created view. Please try again.');
        }
    }
</script>

{#if checkpoint?.checkpointName === 'open-view-menu'}
    <ProductTourSpotlight
        {checkpoint}
        description="Open View settings to review what a reusable saved view can remember."
        onDismiss={actions.dismiss}
        onNext={(active) => {
            openMenu();
            productTourCheckpoint.advance(active, 'review-settings');
        }}
        target="[data-tour='saved-view-trigger']"
        title="Open View settings"
    />
{:else if checkpoint?.checkpointName === 'review-settings'}
    <ProductTourSpotlight
        {checkpoint}
        description="Review the filters, date range, display choices, and columns. The guide will not change them for you."
        onDismiss={actions.dismiss}
        onNext={(active) => {
            closeMenu();
            openSaveDialog();
            productTourCheckpoint.advance(active, 'name-view');
        }}
        target="[data-tour='saved-view-settings']"
        title="Configure what the view remembers"
    />
{:else if checkpoint?.checkpointName === 'name-view'}
    <ProductTourSpotlight
        {checkpoint}
        description="Choose a meaningful name for the current filters, time range, display options, and columns."
        onDismiss={actions.dismiss}
        onNext={(active) => {
            productTourCheckpoint.advance(active, 'private-view');
        }}
        target="[data-tour='saved-view-name']"
        title="Name your view"
    />
{:else if checkpoint?.checkpointName === 'private-view'}
    <ProductTourSpotlight
        {checkpoint}
        description="Enable Private so this guided practice view is visible only to you."
        onDismiss={actions.dismiss}
        onNext={(active) => {
            productTourCheckpoint.advance(active, 'save-view');
        }}
        target="[data-tour='saved-view-private']"
        title="Keep it private"
    />
{:else if checkpoint?.checkpointName === 'save-view'}
    <ProductTourSpotlight
        {checkpoint}
        description="Create the saved view when you are ready. The guide finishes after the view is successfully created and loaded."
        onDismiss={actions.dismiss}
        target="[data-tour='saved-view-submit']"
        title="Create the saved view"
    />
{:else if checkpoint?.phase.type === 'saved-view-created' || checkpoint?.phase.type === 'saved-view-loaded'}
    <ProductTourSpotlight
        {checkpoint}
        continueLabel="Retry guide completion"
        description={checkpoint.phase.type === 'saved-view-created'
            ? 'The view was created, but it could not be loaded. Continue to try loading it again.'
            : 'The view was created and loaded, but guide progress could not be saved. Continue to retry.'}
        onDismiss={actions.dismiss}
        onNext={retry}
        target="[data-tour='saved-view-trigger']"
        title="Finish the saved-view guide"
    />
{/if}
