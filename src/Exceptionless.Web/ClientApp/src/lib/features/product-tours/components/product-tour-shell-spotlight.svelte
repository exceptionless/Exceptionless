<script lang="ts">
    import type { AssistantAccess } from '$features/assistant/models';

    import { onDestroy, onMount, tick, untrack } from 'svelte';

    import type { ProductTourCheckpoint, ProductTourCheckpointName } from '../types';

    import { createProductTourActions } from '../actions.svelte';
    import { productTourCheckpoint } from '../state.svelte';
    import ProductTourSpotlight from './product-tour-spotlight.svelte';

    interface Props {
        assistantAccess?: AssistantAccess;
        checkpoint: ProductTourCheckpoint;
        isAnyOverlayOpen: boolean;
        isMobile: boolean;
        openAssistant: () => Promise<void>;
        setMobileNavigationOpen: (open: boolean) => void;
    }

    interface ShellStep {
        checkpointName: ProductTourCheckpointName;
        description: string;
        mobileNavigation?: boolean;
        target: string;
        title: string;
    }

    let { assistantAccess, checkpoint, isAnyOverlayOpen, isMobile, openAssistant, setMobileNavigationOpen }: Props = $props();
    const currentAssistantAccess = untrack(() => assistantAccess);
    const currentCheckpoint = untrack(() => checkpoint);
    const actions = createProductTourActions();
    const exieOverviewSteps: ShellStep[] = [
        {
            checkpointName: 'open-exie',
            description: 'Open Exie to see the page context available for your next question.',
            target: '[data-tour="exie-trigger"]',
            title: 'Open Exie'
        },
        {
            checkpointName: 'exie-context',
            description: 'Nothing is sent until you choose a prompt. Submitted requests use metered provider usage.',
            target: '[data-tour="exie-panel"]',
            title: 'You control every request'
        }
    ];
    const appOverviewSteps: ShellStep[] = [
        {
            checkpointName: 'navigation',
            description: 'Move between dashboards, saved views, and settings from the application navigation.',
            mobileNavigation: true,
            target: '[data-tour="app-navigation"]',
            title: 'Your workspace navigation'
        },
        {
            checkpointName: 'command-search',
            description: 'Open search or press / to jump to pages, projects, events, stacks, and actions.',
            target: '[data-tour="command-search"]',
            title: 'Find anything quickly'
        },
        {
            checkpointName: 'saved-views',
            description: 'Saved views preserve filters, time, sorting, charts, stats, and columns for quick reuse.',
            mobileNavigation: true,
            target: '[data-tour="saved-view-navigation"]',
            title: 'Reuse configured views'
        },
        ...(currentAssistantAccess?.has_access
            ? [
                  {
                      checkpointName: 'exie' as const,
                      description: 'Exie can investigate the page or error you are viewing. You decide whether to send a prompt.',
                      target: '[data-tour="exie-trigger"]',
                      title: 'Ask Exie with context'
                  }
              ]
            : []),
        {
            checkpointName: 'help',
            description: 'Open Help for documentation, support, keyboard shortcuts, and guided tours.',
            mobileNavigation: true,
            target: '[data-tour="help-menu"]',
            title: 'Help is always nearby'
        }
    ];
    const steps = currentCheckpoint.tourName === 'app-overview' ? appOverviewSteps : exieOverviewSteps;
    const spotlight = steps.find((step) => step.checkpointName === currentCheckpoint.checkpointName);
    let targetReady = $state(false);

    onMount(async () => {
        setMobileNavigationOpen(spotlight?.mobileNavigation ?? false);
        if (isMobile && spotlight?.mobileNavigation) {
            await tick();
        }
        targetReady = true;
    });

    onDestroy(() => {
        setMobileNavigationOpen(false);
    });

    async function advance(): Promise<void> {
        if (currentCheckpoint.tourName === 'exie-overview' && currentCheckpoint.checkpointName === 'open-exie') {
            await openAssistant();
            productTourCheckpoint.advance(currentCheckpoint, 'exie-context');
            return;
        }

        const index = steps.findIndex((step) => step.checkpointName === currentCheckpoint.checkpointName);
        const next = steps[index + 1];
        if (next) {
            productTourCheckpoint.advance(currentCheckpoint, next.checkpointName);
            return;
        }

        await actions.complete(currentCheckpoint);
    }
</script>

{#if spotlight && targetReady && (!isAnyOverlayOpen || checkpoint.tourName === 'exie-overview')}
    <ProductTourSpotlight
        checkpoint={currentCheckpoint}
        description={spotlight.description}
        onDismiss={actions.dismiss}
        onNext={advance}
        stepCount={steps.length}
        stepNumber={steps.indexOf(spotlight) + 1}
        target={spotlight.target}
        title={spotlight.title}
    />
{/if}
