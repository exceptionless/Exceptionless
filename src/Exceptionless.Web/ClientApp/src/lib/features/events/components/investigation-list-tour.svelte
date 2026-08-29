<script lang="ts">
    import { createProductTourActions } from '$features/product-tours/actions.svelte';
    import ProductTourSpotlight from '$features/product-tours/components/product-tour-spotlight.svelte';
    import { productTourCheckpoint } from '$features/product-tours/state.svelte';

    const actions = createProductTourActions();
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'event-investigate' ? productTourCheckpoint.current : undefined);
</script>

{#if checkpoint?.checkpointName === 'filter-errors'}
    <ProductTourSpotlight
        {checkpoint}
        description="Errors are selected. Narrow the list by project, status, date, version, tags, or search terms when you need a specific incident."
        onDismiss={actions.dismiss}
        onNext={(current) => {
            productTourCheckpoint.advance(current, 'choose-error');
        }}
        target="[data-tour='event-filters']"
        title="Start with the right errors"
    />
{:else if checkpoint?.checkpointName === 'choose-error'}
    <ProductTourSpotlight
        {checkpoint}
        description="Choose a real error row. The guide continues only after its detail sheet loads."
        onDismiss={actions.dismiss}
        target="[data-tour='event-list']"
        title="Open a real error"
    />
{/if}
