<script lang="ts">
    import { driver, type Driver } from 'driver.js';
    import { mount, onMount, type Snippet, tick, unmount } from 'svelte';
    import { toast } from 'svelte-sonner';

    import type { ProductTourCheckpoint, ProductTourShortcut } from '../models';

    import { PRODUCT_TOUR_CHECKPOINTS } from '../models';
    import { productTourCheckpoint } from '../state.svelte';
    import ProductTourDescription from './product-tour-description.svelte';
    import 'driver.js/dist/driver.css';

    interface Props {
        checkpoint: ProductTourCheckpoint;
        continueLabel?: string;
        description: Snippet | string;
        onDismiss: (checkpoint: ProductTourCheckpoint) => Promise<boolean>;
        onNext?: (checkpoint: ProductTourCheckpoint) => Promise<void> | void;
        onPrevious?: (checkpoint: ProductTourCheckpoint) => void;
        shortcuts?: ProductTourShortcut[];
        showProgress?: boolean;
        side?: 'bottom' | 'left' | 'right' | 'top';
        stepCount?: number;
        stepNumber?: number;
        target: Element | string;
        title: string;
    }

    let {
        checkpoint,
        continueLabel = 'Continue',
        description,
        onDismiss,
        onNext,
        onPrevious,
        shortcuts,
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

    onMount(() => {
        let cancelled = false;
        let resizeObserver: ResizeObserver | undefined;
        let frame: number | undefined;
        void tick().then(() => {
            if (cancelled) {
                return;
            }
            initialize();
            const element = activeDriver?.getActiveElement();
            if (element) {
                resizeObserver = new ResizeObserver(() => activeDriver?.refresh());
                resizeObserver.observe(element);
            }
            frame = requestAnimationFrame(() => activeDriver?.refresh());
        });

        return () => {
            cancelled = true;
            resizeObserver?.disconnect();
            if (frame !== undefined) {
                cancelAnimationFrame(frame);
            }
            window.removeEventListener('keydown', onKeyDown, true);
            destroy();
        };
    });

    function initialize(): void {
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
            // Handle keys on keydown so an Escape that closes another overlay cannot end the resumed guide on keyup.
            allowKeyboardControl: false,
            animate: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            disableActiveInteraction: false,
            onCloseClick: () => {
                void dismiss();
            },
            onDestroyStarted: () => {
                void dismiss();
            },
            onPopoverRender: (popover) => {
                popover.wrapper.style.display = 'grid';
                popover.closeButton.setAttribute('aria-label', 'End guide');
                popover.closeButton.setAttribute('title', 'End guide');
                popover.progress.textContent = showProgress ? `Step ${currentStepNumber} of ${totalSteps}` : '';
                popover.description.replaceChildren();
                popover.description.style.display = 'block';
                descriptionContent = mount(ProductTourDescription, {
                    props: {
                        description,
                        shortcuts
                    },
                    target: popover.description
                });
            },
            overlayClickBehavior: () => {},
            popoverClass: 'product-tour-popover',
            showProgress,
            smoothScroll: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            steps: [
                {
                    element,
                    popover: {
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
        instance.drive();
        window.addEventListener('keydown', onKeyDown, true);
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
</script>

<style>
    :global(.product-tour-popover.driver-popover) {
        grid-template-columns: minmax(0, 1fr) auto;
        column-gap: 0.5rem;
        padding: 0.75rem;
        max-width: min(24rem, calc(100vw - 2rem));
        border: 1px solid var(--border);
        border-radius: var(--radius-lg);
        background: var(--popover);
        color: var(--popover-foreground);
        box-shadow: 0 10px 15px -3px color-mix(in oklab, var(--foreground) 12%, transparent);
    }

    :global(.product-tour-popover .driver-popover-title) {
        grid-column: 1;
        grid-row: 1;
        align-self: center;
        color: var(--popover-foreground);
        font-size: 1rem;
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
        position: static;
        grid-column: 2;
        grid-row: 1;
        align-self: start;
        width: 2rem;
        height: 2rem;
        background: transparent;
        color: var(--muted-foreground);
        font-size: 1.25rem;
    }

    :global(.product-tour-popover .driver-popover-next-btn) {
        padding: 0.375rem 0.75rem;
        font-size: 0.8125rem;
    }

    :global(.product-tour-popover .driver-popover-close-btn:hover) {
        background: var(--accent);
        color: var(--accent-foreground);
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
