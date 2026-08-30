<script lang="ts">
    import { driver, type Driver } from 'driver.js';
    import { onMount } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { ProductTourCheckpoint } from '../types';

    import { productTourCheckpoint } from '../state.svelte';
    import { PRODUCT_TOUR_CHECKPOINTS } from '../types';
    import 'driver.js/dist/driver.css';

    interface Props {
        checkpoint: ProductTourCheckpoint;
        continueLabel?: string;
        description: string;
        onDismiss: (checkpoint: ProductTourCheckpoint) => Promise<boolean>;
        onNext?: (checkpoint: ProductTourCheckpoint) => Promise<void> | void;
        side?: 'bottom' | 'left' | 'right' | 'top';
        stepCount?: number;
        stepNumber?: number;
        target: Element | string;
        title: string;
    }

    let { checkpoint, continueLabel = 'Continue', description, onDismiss, onNext, side, stepCount, stepNumber, target, title }: Props = $props();
    let activeDriver: Driver | undefined;
    let dismissing = false;
    let returnFocus: HTMLElement | null = null;

    onMount(() => {
        const element = typeof target === 'string' ? document.querySelector(target) : target;
        if (!element) {
            productTourCheckpoint.clear(checkpoint);
            toast.warning('This guide step is unavailable on this page. Start the guide again from Guided Tours.');
            return;
        }

        returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        const checkpoints: readonly string[] = PRODUCT_TOUR_CHECKPOINTS[checkpoint.tourName];
        const currentStepNumber = stepNumber ?? checkpoints.indexOf(checkpoint.checkpointName) + 1;
        const totalSteps = stepCount ?? checkpoints.length;
        const instance = driver({
            allowClose: true,
            animate: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            disableActiveInteraction: false,
            onCloseClick: () => {
                void dismiss();
            },
            onDestroyStarted: () => {
                void dismiss();
            },
            onPopoverRender: (popover) => {
                popover.closeButton.textContent = 'End guide';
                popover.closeButton.setAttribute('aria-label', 'End guide');
                popover.closeButton.setAttribute('title', 'End guide');
                popover.progress.textContent = `Step ${currentStepNumber} of ${totalSteps}`;
            },
            overlayClickBehavior: () => {},
            popoverClass: 'product-tour-popover',
            showProgress: true,
            smoothScroll: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            steps: [
                {
                    element,
                    popover: {
                        description,
                        doneBtnText: continueLabel,
                        onNextClick: onNext
                            ? async () => {
                                  await onNext(checkpoint);
                              }
                            : undefined,
                        showButtons: onNext ? ['close', 'next'] : ['close'],
                        side,
                        title
                    }
                }
            ]
        });
        activeDriver = instance;
        instance.drive();

        return destroyImmediately;
    });

    async function dismiss(): Promise<void> {
        if (dismissing || !activeDriver) {
            return;
        }

        dismissing = true;
        if (await onDismiss(checkpoint)) {
            destroy();
        } else {
            dismissing = false;
        }
    }

    function destroy(): void {
        const instance = activeDriver;
        activeDriver = undefined;
        instance?.setConfig({
            ...instance.getConfig(),
            onDestroyStarted: undefined
        });
        instance?.destroy();
        queueMicrotask(() => returnFocus?.focus());
    }

    function destroyImmediately(): void {
        destroy();
    }
</script>

<style>
    :global(.product-tour-popover.driver-popover) {
        max-width: min(24rem, calc(100vw - 2rem));
        border: 1px solid var(--border);
        border-radius: var(--radius-lg);
        background: var(--popover);
        color: var(--popover-foreground);
        box-shadow: 0 10px 15px -3px color-mix(in oklab, var(--foreground) 12%, transparent);
    }

    :global(.product-tour-popover .driver-popover-title) {
        color: var(--popover-foreground);
        font-size: 1rem;
        font-weight: 600;
        padding-right: 5.5rem;
    }

    :global(.product-tour-popover .driver-popover-description),
    :global(.product-tour-popover .driver-popover-progress-text) {
        color: var(--muted-foreground);
    }

    :global(.product-tour-popover .driver-popover-close-btn),
    :global(.product-tour-popover .driver-popover-next-btn) {
        min-width: 2.75rem;
        min-height: 2.75rem;
        border-color: var(--border);
        border-radius: var(--radius-md);
        background: var(--background);
        color: var(--foreground);
        font-size: 0.875rem;
        text-shadow: none;
    }

    :global(.product-tour-popover .driver-popover-close-btn) {
        top: 0.25rem;
        right: 0.25rem;
        width: auto;
        padding: 0.5rem;
        font-size: 0.75rem;
    }

    :global(.product-tour-popover button:focus-visible) {
        outline: 2px solid var(--ring);
        outline-offset: 2px;
    }

    :global(.product-tour-popover .driver-popover-arrow-side-top) {
        border-bottom-color: var(--popover);
    }

    :global(.product-tour-popover .driver-popover-arrow-side-right) {
        border-left-color: var(--popover);
    }

    :global(.product-tour-popover .driver-popover-arrow-side-bottom) {
        border-top-color: var(--popover);
    }

    :global(.product-tour-popover .driver-popover-arrow-side-left) {
        border-right-color: var(--popover);
    }
</style>
