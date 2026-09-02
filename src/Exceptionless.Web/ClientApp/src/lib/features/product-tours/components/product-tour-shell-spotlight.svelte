<script lang="ts">
    import type { AssistantAccess } from '$features/assistant/models';

    import { appKeyboardShortcuts } from '$features/shared/keyboard-shortcuts';
    import { onDestroy, onMount, tick, untrack } from 'svelte';

    import type { ProductTourCheckpoint, ProductTourCheckpointName } from '../types';
    import type { ProductTourShortcut } from './product-tour-description.svelte';

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
        shortcuts?: ProductTourShortcut[];
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
            description: 'Open Exie to see which page details it can use to answer your question.',
            target: '[data-tour="exie-trigger"]',
            title: 'Open Exie'
        },
        {
            checkpointName: 'exie-context',
            description: 'The guide does not send an AI request. Sending a message or choosing a suggested question counts as AI usage.',
            target: '[data-tour="exie-panel"]',
            title: 'You control every request'
        }
    ];
    const appOverviewSteps: ShellStep[] = [
        {
            checkpointName: 'navigation',
            description: 'Use Stacks for grouped issues and Events for individual occurrences. Switch views in the sidebar.',
            mobileNavigation: true,
            shortcuts: [
                {
                    label: 'Stacks',
                    shortcut: appKeyboardShortcuts.stacks
                },
                {
                    label: 'Events',
                    shortcut: appKeyboardShortcuts.allEvents
                }
            ],
            target: '[data-tour="app-navigation"]',
            title: 'Your workspace navigation'
        },
        {
            checkpointName: 'command-search',
            description: 'Select Search at the top of the page to find pages, projects, events, and actions. Close it to return to this guide.',
            shortcuts: [
                {
                    label: 'Search',
                    shortcut: appKeyboardShortcuts.commandPalette
                }
            ],
            target: '[data-tour="command-search"]',
            title: 'Use the command palette'
        },
        {
            checkpointName: 'saved-views',
            description: 'Expand Stacks or Events to find views. Choose one to restore saved filters and layout, or Continue to keep exploring.',
            mobileNavigation: true,
            target: '[data-tour="saved-view-navigation"]',
            title: 'Find your saved views'
        },
        ...(currentAssistantAccess?.has_access
            ? [
                  {
                      checkpointName: 'exie' as const,
                      description: 'Exie can help investigate this page or error. You choose whether to send an AI request.',
                      target: '[data-tour="exie-trigger"]',
                      title: 'Ask Exie with context'
                  }
              ]
            : []),
        {
            checkpointName: 'help',
            description: 'This is Guided Tours, under your name → Help. Open it whenever you want to try another guide or restart one.',
            mobileNavigation: true,
            shortcuts: [
                {
                    label: 'User menu',
                    shortcut: appKeyboardShortcuts.userMenu
                },
                {
                    label: 'All shortcuts',
                    shortcut: appKeyboardShortcuts.keyboardShortcuts
                }
            ],
            target: '[data-tour="help-menu"]',
            title: 'Find your next guide'
        }
    ];
    const steps = currentCheckpoint.tourName === 'app-overview' ? appOverviewSteps : exieOverviewSteps;
    const spotlight = steps.find((step) => step.checkpointName === currentCheckpoint.checkpointName);
    const stepIndex = steps.findIndex((step) => step.checkpointName === currentCheckpoint.checkpointName);
    let targetReady = $state(false);
    const isHelpStep = currentCheckpoint.tourName === 'app-overview' && currentCheckpoint.checkpointName === 'help';
    const helpTarget = $derived(isHelpStep ? controls?.getGuidedToursTarget() : undefined);

    onMount(async () => {
        if (isMobile || spotlight?.mobileNavigation) {
            setMobileNavigationOpen(spotlight?.mobileNavigation ?? false);
        }

        if (isMobile && spotlight?.mobileNavigation) {
            await tick();
        }

        if (isHelpStep) {
            await controls?.showGuidedToursMenu();
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
            return;
        }

        if (isHelpStep) {
            if (helpTarget) {
                controls?.openCatalog();
            } else {
                await controls?.showGuidedToursMenu();
            }
        } else {
            await actions.complete(currentCheckpoint);
        }
    }

    function back(): void {
        const previous = steps[stepIndex - 1];
        if (previous) {
            if (isHelpStep) {
                controls?.closeOverlays();
            }
            productTourCheckpoint.advance(currentCheckpoint, previous.checkpointName);
        }
    }

    async function dismiss(): Promise<boolean> {
        const dismissed = await actions.dismiss(currentCheckpoint);
        if (dismissed && isHelpStep) {
            controls?.closeOverlays();
        }
        return dismissed;
    }
</script>

{#if spotlight && targetReady && (!isAnyOverlayOpen || helpTarget || checkpoint.tourName === 'exie-overview')}
    {#key helpTarget}
        <ProductTourSpotlight
            checkpoint={currentCheckpoint}
            continueLabel={stepIndex === steps.length - 1
                ? checkpoint.tourName === 'app-overview'
                    ? helpTarget
                        ? 'Browse guides'
                        : 'Show me where'
                    : 'Finish guide'
                : checkpoint.tourName === 'exie-overview'
                  ? 'Open Exie'
                  : 'Continue'}
            description={spotlight.description}
            onDismiss={dismiss}
            onNext={advance}
            onPrevious={checkpoint.tourName === 'app-overview' && stepIndex > 0 ? back : undefined}
            shortcuts={spotlight.shortcuts}
            side={isHelpStep ? 'top' : undefined}
            stepCount={steps.length}
            stepNumber={stepIndex + 1}
            target={helpTarget ?? spotlight.target}
            title={spotlight.title}
        />
    {/key}
{/if}
