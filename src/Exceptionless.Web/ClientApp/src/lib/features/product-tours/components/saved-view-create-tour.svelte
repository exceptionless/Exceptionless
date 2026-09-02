<script lang="ts">
    import { toast } from 'svelte-sonner';

    import { createProductTourActions } from '../actions.svelte';
    import { productTourCheckpoint } from '../state.svelte';
    import ProductTourSpotlight from './product-tour-spotlight.svelte';

    interface Props {
        closeMenu: () => void;
        isMenuOpen: boolean;
        openMenu: () => void;
        openSaveDialog: () => Promise<void>;
    }

    let { closeMenu, isMenuOpen, openMenu, openSaveDialog }: Props = $props();
    let completionPending = $state(false);
    const actions = createProductTourActions();
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'saved-view-create' ? productTourCheckpoint.current : undefined);

    $effect(() => {
        if (isMenuOpen && checkpoint?.checkpointName === 'open-view-menu') {
            productTourCheckpoint.advance(checkpoint, 'review-settings');
        }
    });

    export function openingSaveDialog(): void {
        if (checkpoint?.checkpointName === 'review-settings') {
            productTourCheckpoint.advance(checkpoint, 'name-view');
        }
    }

    export function validateSave(isPrivate: boolean): boolean {
        if (!checkpoint) {
            return true;
        }

        if (checkpoint.checkpointName !== 'save-view') {
            toast.error('Complete the guide steps before saving your view.');
            return false;
        }

        if (isPrivate) {
            return true;
        }

        toast.error('Turn on Private so only you can see this view.');
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
        description="Open View to review the settings you can save."
        continueLabel="Open View"
        onDismiss={actions.dismiss}
        onNext={openMenu}
        target="[data-tour='saved-view-trigger']"
        title="Open View settings"
    />
{:else if checkpoint?.checkpointName === 'review-settings'}
    <ProductTourSpotlight
        {checkpoint}
        continueLabel="Save As…"
        onDismiss={actions.dismiss}
        onPrevious={(active) => {
            closeMenu();
            productTourCheckpoint.advance(active, 'open-view-menu');
        }}
        onNext={async () => {
            closeMenu();
            await openSaveDialog();
        }}
        target="[data-tour='saved-view-save-as']"
        title="Save your current view"
    >
        {#snippet description()}
            Select <strong>Save As…</strong> to name a copy of your current filters and layout. Your existing view stays unchanged.
        {/snippet}
    </ProductTourSpotlight>
{:else if checkpoint?.checkpointName === 'name-view'}
    <ProductTourSpotlight
        {checkpoint}
        description="Choose a name that will help you recognize this view later."
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
        description="Keep Private turned on so only you can see this view."
        onDismiss={actions.dismiss}
        onPrevious={(active) => {
            productTourCheckpoint.advance(active, 'name-view');
        }}
        onNext={(active) => {
            productTourCheckpoint.advance(active, 'save-view');
        }}
        target="[data-tour='saved-view-private']"
        title="Keep it private"
    />
{:else if checkpoint?.checkpointName === 'save-view'}
    <ProductTourSpotlight
        {checkpoint}
        onDismiss={actions.dismiss}
        onPrevious={(active) => {
            productTourCheckpoint.advance(active, 'private-view');
        }}
        target="[data-tour='saved-view-submit']"
        title="Create the saved view"
    >
        {#snippet description()}
            Select <strong>Save</strong> in the form to create your view and finish the guide.
        {/snippet}
    </ProductTourSpotlight>
{:else if checkpoint?.checkpointName === 'view-created' && !completionPending}
    <ProductTourSpotlight
        {checkpoint}
        continueLabel="Retry guide completion"
        description="Your view is saved. Retry saving your guide progress; this will not create another view."
        onDismiss={actions.dismiss}
        onNext={retry}
        target="[data-tour='saved-view-trigger']"
        title="Finish the saved-view guide"
    />
{/if}
