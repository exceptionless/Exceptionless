<script lang="ts">
    import { createProductTourActions } from '$features/product-tours/actions.svelte';
    import ProductTourSpotlight from '$features/product-tours/components/product-tour-spotlight.svelte';
    import { productTourCheckpoint } from '$features/product-tours/state.svelte';

    interface Props {
        firstErrorId?: string;
        onOpenError: (eventId: string) => void;
    }

    let { firstErrorId, onOpenError }: Props = $props();
    const actions = createProductTourActions();
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'event-investigate' ? productTourCheckpoint.current : undefined);

    function openFirstError(): void {
        if (firstErrorId) {
            onOpenError(firstErrorId);
        }
    }
</script>

{#if checkpoint?.checkpointName === 'choose-error'}
    {#key firstErrorId}
        <ProductTourSpotlight
            {checkpoint}
            continueLabel="Open error"
            description={firstErrorId
                ? 'Open an error to see what happened and how often it occurs.'
                : 'There are no errors in this list yet. Try a different time range or project.'}
            onDismiss={actions.dismiss}
            onNext={firstErrorId ? openFirstError : undefined}
            side="bottom"
            target={firstErrorId ? "[data-tour='event-list'] tbody tr[tabindex='0']" : "[data-tour='event-filters']"}
            title="Take a closer look"
        />
    {/key}
{/if}
