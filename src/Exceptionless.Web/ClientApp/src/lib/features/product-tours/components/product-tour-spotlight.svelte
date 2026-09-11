<script lang="ts">
    import { driver, type Driver } from 'driver.js';
    import { mount, type Snippet, tick, unmount, untrack } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { ProductTourCheckpoint } from '../models';

    import { PRODUCT_TOUR_CHECKPOINTS } from '../models';
    import { productTourCheckpoint, productTourPresentation } from '../state.svelte';
    import ProductTourDescription from './product-tour-description.svelte';
    import 'driver.js/dist/driver.css';

    interface Props {
        checkpoint: ProductTourCheckpoint;
        continueLabel?: string;
        description: Snippet | string;
        onDismiss: (checkpoint: ProductTourCheckpoint) => Promise<boolean>;
        onNext?: (checkpoint: ProductTourCheckpoint) => Promise<void> | void;
        onPrevious?: (checkpoint: ProductTourCheckpoint) => void;
        showProgress?: boolean;
        side?: 'bottom' | 'left' | 'right' | 'top';
        stepCount?: number;
        stepNumber?: number;
        target: Element | string;
        title: string;
    }

    let {
        checkpoint,
        continueLabel = 'Next',
        description,
        onDismiss,
        onNext,
        onPrevious,
        showProgress = true,
        side,
        stepCount,
        stepNumber,
        target,
        title
    }: Props = $props();
    let activeDriver: Driver | undefined;
    let descriptionContent: ReturnType<typeof mount> | undefined;
    let dismissing = false;
    let returnFocus: HTMLElement | null = null;

    $effect(() => {
        if (productTourPresentation.suspended) {
            return;
        }

        return untrack(() => {
            const controller = new AbortController();
            initialize();
            window.addEventListener('keydown', onKeyDown, {
                capture: true,
                signal: controller.signal
            });
            let frame = 0;
            let previousBounds = '';
            function followTarget(): void {
                const element = activeDriver?.getActiveElement();
                const popover = activeDriver?.getState().popover?.wrapper;
                if (!element || !popover) {
                    return;
                }

                // A refreshed list can replace a row while keeping the same report selected.
                if (!element.isConnected) {
                    popover.style.visibility = 'hidden';
                    if (getTarget()?.isConnected) {
                        const previousFocus = returnFocus;
                        destroy();
                        initialize();
                        returnFocus = previousFocus;
                        previousBounds = '';
                    }
                    frame = requestAnimationFrame(followTarget);
                    return;
                }

                // Menus and drawers can move without resizing. Keep the arrow and spotlight attached.
                const bounds = element.getBoundingClientRect();
                const currentBounds = [bounds.x, bounds.y, bounds.width, bounds.height, popover.offsetWidth, popover.offsetHeight].join(',');
                if (currentBounds !== previousBounds) {
                    previousBounds = currentBounds;
                    activeDriver?.refresh();
                }
                frame = requestAnimationFrame(followTarget);
            }
            frame = requestAnimationFrame(followTarget);

            return () => {
                controller.abort();
                cancelAnimationFrame(frame);
                destroy();
            };
        });
    });

    function destroy(): void {
        const instance = activeDriver;
        activeDriver = undefined;
        if (descriptionContent) {
            void unmount(descriptionContent);
            descriptionContent = undefined;
        }

        instance?.setConfig({
            ...instance.getConfig(),
            onDestroyStarted: undefined
        });
        instance?.destroy();
        if (!productTourCheckpoint.current && returnFocus?.isConnected) {
            returnFocus.focus();
        }
    }

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

    function getTarget(): Element | undefined {
        return typeof target === 'string' ? [...document.querySelectorAll(target)].find((candidate) => candidate.checkVisibility?.() ?? true) : target;
    }

    function initialize(): void {
        const element = getTarget();
        if (!element) {
            productTourCheckpoint.clear(checkpoint);
            toast.warning('This part of the page is unavailable. You can restart the tour from Search.');
            return;
        }

        returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        const checkpoints: readonly string[] = PRODUCT_TOUR_CHECKPOINTS[checkpoint.tourName];
        const currentStepNumber = stepNumber ?? checkpoints.indexOf(checkpoint.checkpointName) + 1;
        const totalSteps = stepCount ?? checkpoints.length;
        const isForm = element instanceof HTMLFormElement;
        const instance = driver({
            allowClose: true,
            // Handle keys on keydown so an Escape that closes another overlay cannot end the resumed guide on keyup.
            allowKeyboardControl: false,
            animate: false,
            disableActiveInteraction: false,
            onCloseClick: () => {
                void dismiss();
            },
            onDestroyStarted: () => {
                void dismiss();
            },
            onPopoverRender: (popover) => {
                popover.wrapper.style.display = 'grid';
                popover.wrapper.style.visibility = 'hidden';
                popover.closeButton.setAttribute('aria-label', 'End guide');
                popover.closeButton.setAttribute('title', 'End guide');
                popover.progress.textContent = showProgress ? `Step ${currentStepNumber} of ${totalSteps}` : '';
                popover.description.replaceChildren();
                popover.description.style.display = 'block';
                descriptionContent = mount(ProductTourDescription, {
                    props: {
                        description
                    },
                    target: popover.description
                });
                void tick().then(() => {
                    if (activeDriver === instance) {
                        instance.refresh();
                        popover.wrapper.style.visibility = 'visible';
                    }
                });
            },
            overlayClickBehavior: () => {},
            overlayOpacity: 0.4,
            popoverClass: 'product-tour-popover',
            popoverOffset: 14,
            showProgress,
            smoothScroll: false,
            stagePadding: isForm ? 12 : 6,
            stageRadius: isForm ? 12 : 8,
            steps: [
                {
                    element,
                    popover: {
                        align: 'center',
                        disableButtons: [],
                        doneBtnText: continueLabel,
                        onNextClick: onNext
                            ? async () => {
                                  await onNext(checkpoint);
                              }
                            : undefined,
                        onPrevClick: onPrevious ? () => onPrevious(checkpoint) : undefined,
                        prevBtnText: 'Back',
                        showButtons: ['close', ...(onPrevious ? ['previous' as const] : []), ...(onNext ? ['next' as const] : [])],
                        side,
                        title
                    }
                }
            ]
        });
        activeDriver = instance;
        // Centering a wide table row would scroll its report name out of view on mobile.
        const horizontalScroll: { element: HTMLElement; scrollLeft: number }[] = [];
        for (let parent = element.parentElement; parent; parent = parent.parentElement) {
            if (parent.scrollWidth > parent.clientWidth && element.getBoundingClientRect().width > parent.clientWidth) {
                horizontalScroll.push({
                    element: parent,
                    scrollLeft: parent.scrollLeft
                });
            }
        }
        instance.drive();
        for (const { element, scrollLeft } of horizontalScroll) {
            element.scrollLeft = scrollLeft;
        }
    }

    function onKeyDown(event: KeyboardEvent): void {
        if (event.defaultPrevented || event.repeat || !activeDriver) {
            return;
        }

        if (event.key === 'Escape') {
            event.preventDefault();
            void dismiss();
        } else if (event.target instanceof HTMLButtonElement && event.target.closest('.product-tour-popover')) {
            if (event.key === 'ArrowLeft' && onPrevious) {
                event.preventDefault();
                onPrevious(checkpoint);
            } else if (event.key === 'ArrowRight' && onNext) {
                event.preventDefault();
                void onNext(checkpoint);
            }
        }
    }
</script>

<style>
    :global(.product-tour-popover.driver-popover) {
        grid-template-columns: minmax(0, 1fr);
        padding: 1rem;
        width: 20rem;
        min-width: 0;
        max-width: calc(100vw - 2rem);
        font-family: inherit;
        line-height: 1.5;
        border: 1px solid var(--border);
        border-radius: var(--radius-lg);
        background: var(--popover);
        color: var(--popover-foreground);
        box-shadow: 0 12px 32px -8px rgb(0 0 0 / 0.3);
    }

    :global(.product-tour-popover .driver-popover-title) {
        grid-column: 1;
        grid-row: 1;
        align-self: center;
        padding-right: 2rem;
        color: var(--popover-foreground);
        font-size: 1rem;
        line-height: 1.4;
        font-weight: 600;
    }

    :global(.product-tour-popover .driver-popover-description),
    :global(.product-tour-popover .driver-popover-footer) {
        grid-column: 1 / -1;
    }

    :global(.product-tour-popover .driver-popover-description),
    :global(.product-tour-popover .driver-popover-progress-text) {
        color: var(--muted-foreground);
    }

    :global(.product-tour-popover .driver-popover-close-btn),
    :global(.product-tour-popover .driver-popover-prev-btn),
    :global(.product-tour-popover .driver-popover-next-btn) {
        min-width: 2rem;
        min-height: 2rem;
        border-color: var(--border);
        border-radius: var(--radius-md);
        background: var(--background);
        color: var(--foreground);
        font-size: 0.875rem;
        text-shadow: none;
    }

    :global(.product-tour-popover .driver-popover-prev-btn) {
        border-color: transparent;
        background: transparent;
        color: var(--muted-foreground);
        padding: 0.375rem 0.75rem;
    }

    :global(.product-tour-popover .driver-popover-prev-btn:hover) {
        background: var(--accent);
        color: var(--accent-foreground);
    }

    :global(.product-tour-popover .driver-popover-close-btn) {
        position: absolute;
        top: 0.25rem;
        right: 0.25rem;
        width: 2rem;
        height: 2rem;
        background: transparent;
        color: var(--muted-foreground);
        font-size: 1rem;
        font-weight: 400;
        opacity: 0.6;
    }

    :global(.product-tour-popover .driver-popover-next-btn) {
        padding: 0.5rem 0.875rem;
        border-color: var(--primary);
        background: var(--primary);
        color: var(--primary-foreground);
        font-weight: 500;
    }

    :global(.product-tour-popover .driver-popover-close-btn:hover),
    :global(.product-tour-popover .driver-popover-close-btn:focus-visible) {
        background: var(--accent);
        color: var(--accent-foreground);
        opacity: 1;
    }

    @media (pointer: coarse) {
        :global(.product-tour-popover .driver-popover-close-btn),
        :global(.product-tour-popover .driver-popover-prev-btn),
        :global(.product-tour-popover .driver-popover-next-btn) {
            min-width: 2.75rem;
            min-height: 2.75rem;
        }
    }

    :global(.product-tour-popover button:focus-visible) {
        outline: 2px solid var(--ring);
        outline-offset: 2px;
    }

    :global(.product-tour-popover .driver-popover-arrow) {
        border-width: 8px;
        filter: drop-shadow(0 1px 1px var(--border));
    }

    :global(.product-tour-popover .driver-popover-progress-text) {
        font-size: 0.75rem;
        font-variant-numeric: tabular-nums;
    }

    :global(.driver-active-element) {
        outline: 2px solid var(--primary);
        /* Keep the border visible when a control sits at the edge of a scroll container. */
        outline-offset: -2px;
    }

    :global(form.driver-active-element) {
        border-radius: 4px;
        outline-offset: 8px;
    }

    :global(.product-tour-popover .driver-popover-arrow-side-top) {
        border-top-color: var(--popover);
    }

    :global(.product-tour-popover .driver-popover-arrow-side-right) {
        border-right-color: var(--popover);
    }

    :global(.product-tour-popover .driver-popover-arrow-side-bottom) {
        border-bottom-color: var(--popover);
    }

    :global(.product-tour-popover .driver-popover-arrow-side-left) {
        border-left-color: var(--popover);
    }
</style>
