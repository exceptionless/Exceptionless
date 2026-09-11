<script lang="ts">
    import type { AssistantAccess } from '$features/assistant/models';

    import { onDestroy, onMount, untrack } from 'svelte';

    import type { ProductTourCheckpoint, ProductTourCheckpointName } from '../models';

    import { createProductTourActions } from '../actions.svelte';
    import { tryUseProductTourControls } from '../controls.svelte';
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
    const controls = tryUseProductTourControls();
    const currentAssistantAccess = untrack(() => assistantAccess);
    const currentCheckpoint = untrack(() => checkpoint);
    const actions = createProductTourActions();
    const exieOverviewSteps: ShellStep[] = [
        {
            checkpointName: 'open-exie',
            description: 'Ask Exie to explain an error or help you find patterns in your app.',
            target: '[data-tour="exie-trigger"]',
            title: 'Get help from Exie'
        },
        {
            checkpointName: 'exie-context',
            description: 'Start with a question, or choose a suggestion. Exie can use the page you’re viewing to help.',
            target: '[data-tour="exie-input"]',
            title: 'Ask your first question'
        }
    ];
    const appOverviewSteps: ShellStep[] = [
        {
            checkpointName: 'navigation',
            description: 'Stacks groups similar reports so you can see which problems happen most often.',
            mobileNavigation: true,
            target: '[data-tour="navigation-stacks"]',
            title: 'Spot repeated problems'
        },
        {
            checkpointName: 'events',
            description: 'Events shows each error, log message, and activity your app sends. Open a report to see what happened.',
            mobileNavigation: true,
            target: '[data-tour="navigation-events"]',
            title: 'See each report'
        },
        {
            checkpointName: 'filters',
            description: 'Choose a project, time range, or event type to focus on the reports you need.',
            target: '[data-tour="event-filters"]',
            title: 'Narrow your results'
        },
        {
            checkpointName: 'saved-views',
            description: 'Use View to save the filters you use often. Your saved views appear in the sidebar.',
            target: '[data-tour="saved-view-trigger"]',
            title: 'Keep a useful view'
        },
        ...(currentAssistantAccess?.has_access
            ? [
                  {
                      checkpointName: 'exie' as const,
                      description: 'Ask Exie to explain an error or help you find patterns in your app.',
                      target: '[data-tour="exie-trigger"]',
                      title: 'Get help from Exie'
                  }
              ]
            : []),
        {
            checkpointName: 'command-search',
            description: 'Use the command palette to find stacks and events, jump to pages and projects, and run app commands.',
            target: '[data-tour="command-search"]',
            title: 'Search and take action'
        }
    ];
    const steps = currentCheckpoint.tourName === 'app-overview' ? appOverviewSteps : exieOverviewSteps;
    const stepIndex = steps.findIndex((step) => step.checkpointName === currentCheckpoint.checkpointName);
    const spotlight = steps[stepIndex];
    let targetReady = $state(false);
    const navigationReady = $derived(!isMobile || !spotlight?.mobileNavigation || !!controls?.getNavigationTarget());

    onMount(() => {
        if (currentCheckpoint.tourName === 'app-overview' && currentCheckpoint.checkpointName === 'exie' && !currentAssistantAccess?.has_access) {
            productTourCheckpoint.advance(currentCheckpoint, 'command-search');
            return;
        }

        if (isMobile || spotlight?.mobileNavigation) {
            setMobileNavigationOpen(spotlight?.mobileNavigation ?? false);
        }
        targetReady = true;
    });

    onDestroy(() => {
        if (isMobile) {
            setMobileNavigationOpen(false);
        }
    });

    async function advance(): Promise<void> {
        if (currentCheckpoint.tourName === 'exie-overview' && currentCheckpoint.checkpointName === 'open-exie') {
            await openAssistant();
            productTourCheckpoint.advance(currentCheckpoint, 'exie-context');
            return;
        }

        const next = steps[stepIndex + 1];
        if (next) {
            productTourCheckpoint.advance(currentCheckpoint, next.checkpointName);
        } else {
            await actions.complete(currentCheckpoint);
        }
    }

    function back(): void {
        const previous = steps[stepIndex - 1];
        if (previous) {
            productTourCheckpoint.advance(currentCheckpoint, previous.checkpointName);
        }
    }
</script>

{#if spotlight && targetReady && navigationReady && (!isAnyOverlayOpen || checkpoint.tourName === 'exie-overview')}
    <ProductTourSpotlight
        checkpoint={currentCheckpoint}
        continueLabel={stepIndex === steps.length - 1 ? 'Done' : checkpoint.tourName === 'exie-overview' ? 'Open Exie' : 'Next'}
        description={spotlight.description}
        onDismiss={actions.dismiss}
        onNext={advance}
        onPrevious={stepIndex > 0 ? back : undefined}
        side={spotlight.mobileNavigation && !isMobile ? 'right' : 'bottom'}
        stepCount={steps.length}
        stepNumber={stepIndex + 1}
        target={spotlight.target}
        title={spotlight.title}
    />
{/if}
