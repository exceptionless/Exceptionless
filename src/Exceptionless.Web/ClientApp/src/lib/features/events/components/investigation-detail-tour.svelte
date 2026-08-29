<script lang="ts">
    import { createProductTourActions } from '$features/product-tours/actions.svelte';
    import ProductTourInlineCallout from '$features/product-tours/components/product-tour-inline-callout.svelte';
    import { productTourCheckpoint } from '$features/product-tours/state.svelte';

    import type { PersistentEvent } from '../models';

    import { hasErrorOrSimpleError } from '../persistent-event';

    interface Props {
        event?: PersistentEvent;
        placement: 'occurrence' | 'overview' | 'stack';
    }

    let { event, placement }: Props = $props();
    let advancedEventId = $state('');

    const actions = createProductTourActions();
    const checkpoint = $derived(productTourCheckpoint.current?.tourName === 'event-investigate' ? productTourCheckpoint.current : undefined);
    const copy = $derived.by(() => {
        switch (checkpoint?.checkpointName) {
            case 'event-occurrence':
                return placement === 'occurrence'
                    ? {
                          description: 'This occurrence contains its timestamp, raw JSON, and navigation to nearby events.',
                          title: 'Inspect the occurrence'
                      }
                    : undefined;
            case 'filter-stack-events':
                return placement === 'occurrence'
                    ? {
                          description: 'Show all events filters the list to this stack when you are ready to compare occurrences.',
                          title: 'Compare every occurrence'
                      }
                    : undefined;
            case 'stack-summary':
                return placement === 'stack'
                    ? {
                          description: 'Use the grouped stack title, affected users, occurrence count, and trend to judge scope and impact.',
                          title: 'Understand the grouped issue'
                      }
                    : undefined;
            case 'stack-triage':
                return placement === 'stack'
                    ? {
                          description: 'Status and options change shared issue state. Review them here; this guide will not invoke them.',
                          title: 'Triage deliberately'
                      }
                    : undefined;
            case 'tab-overview':
                return placement === 'overview'
                    ? {
                          description: 'Overview summarizes the message and useful event fields. Choose other tabs when the evidence calls for them.',
                          title: 'Begin with the overview'
                      }
                    : undefined;
            default:
                return undefined;
        }
    });

    $effect(() => {
        if (placement !== 'stack' || !event || event.id === advancedEventId) {
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

        switch (active.checkpointName) {
            case 'event-occurrence':
                productTourCheckpoint.advance(active, 'tab-overview');
                break;
            case 'stack-summary':
                productTourCheckpoint.advance(active, 'stack-triage');
                break;
            case 'stack-triage':
                productTourCheckpoint.advance(active, 'event-occurrence');
                break;
            case 'tab-overview':
                productTourCheckpoint.advance(active, 'filter-stack-events');
                break;
            default:
                await actions.complete(active);
        }
    }

    async function dismiss(): Promise<void> {
        if (checkpoint) {
            await actions.dismiss(checkpoint);
        }
    }
</script>

{#if event && checkpoint && copy}
    <ProductTourInlineCallout
        continueLabel={checkpoint.checkpointName === 'filter-stack-events' ? 'Finish guide' : 'Continue'}
        description={copy.description}
        onContinue={continueTour}
        onDismiss={dismiss}
        title={copy.title}
        tourName="event-investigate"
    />
{/if}
