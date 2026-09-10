<script lang="ts">
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

    export function shouldDefaultPrivate(): boolean {
        return Boolean(checkpoint);
    }

    export async function created(): Promise<void> {
        const active = checkpoint;
        if (!active) {
            return;
        }

        await actions.complete(active);
    }

    export async function closed(): Promise<void> {
        const active = checkpoint;
        if (active && active.checkpointName === 'name-view') {
            await actions.dismiss(active);
        }
    }
</script>

{#if checkpoint?.checkpointName === 'open-view-menu'}
    <ProductTourSpotlight
        {checkpoint}
        description="Save the filters you use often so you can return to them in one click."
        continueLabel="Open View"
        onDismiss={actions.dismiss}
        onNext={openMenu}
        target="[data-tour='saved-view-trigger']"
        title="Keep a useful view"
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
            Choose <strong>Save As…</strong> to give your current view a name.
        {/snippet}
    </ProductTourSpotlight>
{:else if checkpoint?.checkpointName === 'name-view'}
    <ProductTourSpotlight
        {checkpoint}
        description="Give this view a name, then select Save. Leave Private on if it’s just for you."
        onDismiss={actions.dismiss}
        side="bottom"
        target="[data-tour='saved-view-form']"
        title="Name it and save"
    />
{/if}
