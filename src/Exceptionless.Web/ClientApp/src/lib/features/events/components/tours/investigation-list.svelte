<script lang="ts">
    import type { ProductTourCheckpoint } from '$features/product-tours/models';

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

    function chooseError(current: ProductTourCheckpoint): void {
        productTourCheckpoint.advance(current, 'choose-error');
    }

    function reviewFilters(current: ProductTourCheckpoint): void {
        productTourCheckpoint.advance(current, 'filter-errors');
    }

    function openFirstError(): void {
        if (firstErrorId) {
            onOpenError(firstErrorId);
        }
    }
</script>

{#if checkpoint?.checkpointName === 'filter-errors'}
    <ProductTourSpotlight
        {checkpoint}
        description="This list shows errors. Use the filters or search to find the one you want to investigate."
        onDismiss={actions.dismiss}
        onNext={chooseError}
        target="[data-tour='event-filters']"
        title="Start with the right errors"
    />
{:else if checkpoint?.checkpointName === 'choose-error'}
    {#key firstErrorId}
        <ProductTourSpotlight
            {checkpoint}
            continueLabel="Open first error"
            description={firstErrorId
                ? 'Open the first error below, or select another row. The guide continues in the details panel.'
                : 'No errors are ready to open in this list. Adjust the filters or wait for the results to load.'}
            onDismiss={actions.dismiss}
            onPrevious={reviewFilters}
            onNext={firstErrorId ? openFirstError : undefined}
            target="[data-tour='event-list']"
            title="Open an error"
        />
    {/key}
{/if}
