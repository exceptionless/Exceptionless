<script lang="ts">
    import { toast } from 'svelte-sonner';

    import { createProductTourActions } from '../actions.svelte';
    import { productTourCheckpoint } from '../state.svelte';
    import ProductTourSpotlight from './product-tour-spotlight.svelte';

    interface Props {
        closeMenu: () => void;
        openMenu: () => void;
        openSaveDialog: () => void;
    }

    let { closeMenu, openMenu, openSaveDialog }: Props = $props();
    let completionPending = $state(false);
    const actions = createProductTourActions();
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'saved-view-create' ? productTourCheckpoint.current : undefined);

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

    export async function created(): Promise<void> {
        const active = checkpoint;
        if (!active || active.checkpointName !== 'save-view') {
            return;
        }

        const createdCheckpoint = productTourCheckpoint.advance(active, 'view-created');
        if (createdCheckpoint) {
            completionPending = true;
            await actions.complete(createdCheckpoint);
            completionPending = false;
        }
    }

    export async function closed(): Promise<void> {
        const active = checkpoint;
        if (active && ['name-view', 'private-view', 'save-view'].includes(active.checkpointName)) {
            await actions.dismiss(active);
        }
    }

    async function retry(): Promise<void> {
        const active = checkpoint;
        if (active?.checkpointName !== 'view-created') {
            return;
        }

        await actions.complete(active);
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
{:else if checkpoint?.checkpointName === 'view-created' && !completionPending}
    <ProductTourSpotlight
        {checkpoint}
        continueLabel="Retry guide completion"
        description="The view was created and loaded, but guide progress could not be saved. Continue to retry."
        onDismiss={actions.dismiss}
        onNext={retry}
        target="[data-tour='saved-view-trigger']"
        title="Finish the saved-view guide"
    />
{/if}
