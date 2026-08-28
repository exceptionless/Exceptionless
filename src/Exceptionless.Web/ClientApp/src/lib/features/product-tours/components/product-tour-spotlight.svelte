<script lang="ts">
    import { driver, type Driver } from 'driver.js';
    import { onMount } from 'svelte';

    import type { ProductTourCheckpoint } from '../types';

    import 'driver.js/dist/driver.css';

    interface Props {
        checkpoint: ProductTourCheckpoint;
        description: string;
        onDismiss: (checkpoint: ProductTourCheckpoint) => Promise<boolean>;
        onNext?: (checkpoint: ProductTourCheckpoint) => Promise<void> | void;
        side?: 'bottom' | 'left' | 'right' | 'top';
        target: Element | string;
        title: string;
    }

    let { checkpoint, description, onDismiss, onNext, side, target, title }: Props = $props();
    let activeDriver: Driver | undefined;

    onMount(() => {
        const instance = driver({
            allowClose: true,
            animate: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            disableActiveInteraction: false,
            onDestroyStarted: async () => {
                if (activeDriver === instance && (await onDismiss(checkpoint))) {
                    destroy();
                }
            },
            popoverClass:
                'max-w-sm rounded-lg border border-border bg-popover text-popover-foreground shadow-lg [&_.driver-popover-title]:text-base [&_.driver-popover-title]:font-semibold',
            showProgress: false,
            smoothScroll: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
            steps: [
                {
                    element: target,
                    popover: {
                        description,
                        doneBtnText: 'Continue',
                        onNextClick: onNext
                            ? async () => {
                                  await onNext(checkpoint);
                              }
                            : undefined,
                        showButtons: onNext ? ['close', 'next'] : ['close'],
                        side,
                        title
                    },
                    waitForElement: 5000
                }
            ]
        });
        activeDriver = instance;
        instance.drive();

        return destroyImmediately;
    });

    function destroy(): void {
        const instance = activeDriver;
        activeDriver = undefined;
        instance?.setConfig({
            ...instance.getConfig(),
            onDestroyStarted: undefined
        });
        instance?.destroy();
    }

    function destroyImmediately(): void {
        destroy();
    }
</script>
