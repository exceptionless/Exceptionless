<script lang="ts">
    import Check from '@lucide/svelte/icons/check';

    import { cn } from '#lib/utils.ts';

    import type { OnboardingStep } from '../models';

    import { ONBOARDING_STEPS } from '../steps';

    interface Props {
        class?: string;
        /** The step the user is on. Earlier steps show as done. */
        current?: OnboardingStep;
        /** Mark the current step as done too (for example, after the first event arrives). */
        currentComplete?: boolean;
        orientation?: 'horizontal' | 'vertical';
        /** Show each step's one-line description (vertical layout only). */
        showDescriptions?: boolean;
    }

    let { class: className, current, currentComplete = false, orientation = 'horizontal', showDescriptions = false }: Props = $props();

    const currentIndex = $derived(current ? ONBOARDING_STEPS.findIndex((step) => step.id === current) : -1);
</script>

<nav aria-label="Setup progress" class={className}>
    <ol class={cn('flex gap-2', orientation === 'vertical' ? 'flex-col gap-3' : 'items-center')}>
        {#each ONBOARDING_STEPS as step, index (step.id)}
            {@const isDone = index < currentIndex || (index === currentIndex && currentComplete)}
            {@const isCurrent = index === currentIndex && !currentComplete}
            <li
                aria-current={isCurrent ? 'step' : undefined}
                class={cn(
                    'flex min-w-0 items-center gap-2',
                    orientation === 'horizontal' && index < ONBOARDING_STEPS.length - 1 && 'flex-1',
                    isCurrent && 'min-w-fit'
                )}
            >
                <span
                    class={cn(
                        'flex size-6 shrink-0 items-center justify-center rounded-full border text-xs font-semibold transition-colors',
                        isDone && 'border-primary bg-primary text-primary-foreground',
                        isCurrent && 'border-primary text-foreground ring-primary/25 ring-4',
                        !isDone && !isCurrent && 'border-border text-muted-foreground'
                    )}
                >
                    {#if isDone}
                        <Check aria-hidden="true" class="size-3.5" strokeWidth={3} />
                    {:else}
                        {index + 1}
                    {/if}
                </span>
                {#if orientation === 'vertical' && showDescriptions}
                    <span class="flex min-w-0 flex-col">
                        <span class="text-foreground text-sm font-medium">{step.label}</span>
                        <span class="text-muted-foreground text-sm">{step.description}</span>
                    </span>
                {:else}
                    <span
                        class={cn(
                            'text-sm whitespace-nowrap',
                            isCurrent ? 'text-foreground font-medium' : 'text-muted-foreground',
                            orientation === 'horizontal' && !isCurrent && 'sr-only sm:not-sr-only'
                        )}
                    >
                        {step.label}
                        {#if isDone}<span class="sr-only"> (done)</span>{/if}
                    </span>
                {/if}
                {#if orientation === 'horizontal' && index < ONBOARDING_STEPS.length - 1}
                    <span aria-hidden="true" class={cn('h-px min-w-3 flex-1', isDone ? 'bg-primary' : 'bg-border')}></span>
                {/if}
            </li>
        {/each}
    </ol>
</nav>
