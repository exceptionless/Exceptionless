<script lang="ts">
    import { createProductTourActions } from '$features/product-tours/actions.svelte';
    import ProductTourSpotlight from '$features/product-tours/components/product-tour-spotlight.svelte';
    import { PRODUCT_TOUR_CHECKPOINTS } from '$features/product-tours/models';
    import { productTourCheckpoint } from '$features/product-tours/state.svelte';

    import type { PersistentEvent } from '../../models';

    import { hasErrorOrSimpleError } from '../../persistent-event';

    interface Props {
        event?: PersistentEvent;
    }

    let { event }: Props = $props();
    let advancedEventId = $state('');

    const actions = createProductTourActions();
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'event-investigate' ? productTourCheckpoint.current : undefined);
    const steps = PRODUCT_TOUR_CHECKPOINTS['event-investigate'];
    const stepIndex = $derived(checkpoint ? steps.indexOf(checkpoint.checkpointName) : -1);
    const copy = $derived.by(() => {
        switch (checkpoint?.checkpointName) {
            case 'event-occurrence':
                return {
                    description: 'An occurrence is one event. This is when it happened. Use the buttons beside Event to view JSON or browse older occurrences.',
                    target: '[data-tour="event-occurrence"]',
                    title: 'Inspect the occurrence'
                };
            case 'filter-stack-events':
                return {
                    description: 'Select “Show all events” to compare occurrences of this stack. You can also finish this guide and keep exploring this event.',
                    target: '[data-tour="stack-events"]',
                    title: 'Compare every occurrence'
                };
            case 'stack-summary':
                return {
                    description: 'A stack groups similar events. Check its event count and users affected.',
                    target: '[data-tour="stack-metrics"]',
                    title: 'Understand the grouped issue'
                };
            case 'stack-triage':
                return {
                    description: 'Status changes affect everyone in the project. This guide does not change the status.',
                    target: '[data-tour="stack-status"]',
                    title: 'Review the issue status'
                };
            case 'tab-overview':
                return {
                    description: 'Select “Overview” to read the message and event details. Other tabs show more context.',
                    target: '[data-tour="event-overview"]',
                    title: 'Begin with the overview'
                };
            default:
                return undefined;
        }
    });

    $effect(() => {
        if (!event || event.id === advancedEventId) {
            return;
        }

        advancedEventId = event.id;
        const active = checkpoint;
        if (active?.checkpointName === 'choose-error' && hasErrorOrSimpleError(event)) {
            productTourCheckpoint.advance(active, 'stack-summary');
        }
    });

    async function continueTour(): Promise<void> {
        const active = checkpoint;
        if (!active) {
            return;
        }

        const next = steps[stepIndex + 1];
        if (next) {
            productTourCheckpoint.advance(active, next);
        } else {
            await actions.complete(active);
        }
    }

    function back(): void {
        const previous = steps[stepIndex - 1];
        if (checkpoint && previous && stepIndex > steps.indexOf('stack-summary')) {
            productTourCheckpoint.advance(checkpoint, previous);
        }
    }
</script>

{#if event && checkpoint && copy}
    {#key checkpoint}
        <ProductTourSpotlight
            {checkpoint}
            continueLabel={checkpoint.checkpointName === 'filter-stack-events' ? 'Finish guide' : 'Continue'}
            description={copy.description}
            onNext={continueTour}
            onPrevious={stepIndex > steps.indexOf('stack-summary') ? back : undefined}
            onDismiss={actions.dismiss}
            target={copy.target}
            title={copy.title}
        />
    {/key}
{/if}
