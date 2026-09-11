<script lang="ts">
    import { createProductTourActions } from '$features/product-tours/actions.svelte';
    import ProductTourSpotlight from '$features/product-tours/components/product-tour-spotlight.svelte';
    import { PRODUCT_TOUR_CHECKPOINTS } from '$features/product-tours/models';
    import { productTourCheckpoint } from '$features/product-tours/state.svelte';

    import type { PersistentEvent } from '../../models';

    import { hasErrorOrSimpleError } from '../../persistent-event';

    interface Props {
        event?: PersistentEvent;
        onCompareEvents: () => Promise<void>;
    }

    let { event, onCompareEvents }: Props = $props();

    const actions = createProductTourActions();
    const firstDetailCheckpoint = 'stack-summary';
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'event-investigate' ? productTourCheckpoint.current : undefined);
    const steps = PRODUCT_TOUR_CHECKPOINTS['event-investigate'];
    const stepIndex = $derived(checkpoint ? steps.indexOf(checkpoint.checkpointName) : -1);
    const copy = $derived.by(() => {
        switch (checkpoint?.checkpointName) {
            case 'filter-stack-events':
                return {
                    description: 'Use Show all events to see the other reports of this same problem.',
                    target: '[data-tour="stack-events"]',
                    title: 'See related reports'
                };
            case firstDetailCheckpoint:
                return {
                    description: 'See how often this problem happens and how many people it affects.',
                    target: '[data-tour="stack-metrics"]',
                    title: 'See the impact'
                };
            case 'tab-overview':
                return {
                    description: 'Overview has the error message and details about where it happened.',
                    target: '[data-tour="event-overview"]',
                    title: 'Read what happened'
                };
            default:
                return undefined;
        }
    });

    $effect(() => {
        const active = checkpoint;
        if (active?.checkpointName === 'choose-error' && event && hasErrorOrSimpleError(event)) {
            productTourCheckpoint.advance(active, firstDetailCheckpoint);
        }
    });

    export async function completeComparison(): Promise<void> {
        if (checkpoint?.checkpointName === 'filter-stack-events') {
            await actions.complete(checkpoint);
        }
    }

    function back(): void {
        const previous = steps[stepIndex - 1];
        if (checkpoint && previous && stepIndex > steps.indexOf(firstDetailCheckpoint)) {
            productTourCheckpoint.advance(checkpoint, previous);
        }
    }

    async function continueTour(): Promise<void> {
        const active = checkpoint;
        if (!active) {
            return;
        }

        const next = steps[stepIndex + 1];
        if (next) {
            productTourCheckpoint.advance(active, next);
        } else {
            await onCompareEvents();
        }
    }
</script>

{#if event && checkpoint && copy}
    {#key checkpoint}
        <ProductTourSpotlight
            {checkpoint}
            continueLabel={checkpoint.checkpointName === 'filter-stack-events' ? 'Show all events' : 'Next'}
            description={copy.description}
            onNext={continueTour}
            onPrevious={stepIndex > steps.indexOf(firstDetailCheckpoint) ? back : undefined}
            onDismiss={actions.dismiss}
            side="bottom"
            target={copy.target}
            title={copy.title}
        />
    {/key}
{/if}
