<script lang="ts">
    import DateTime from '$comp/formatters/date-time.svelte';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { extractRangeExpressions, parseDateMath } from '$features/shared/utils/datemath';
    import Check from '@lucide/svelte/icons/check';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';

    type Props = {
        cancel?: () => void;
        class?: string;
        onselect?: (value: string) => void;
        value?: Date | string;
    };

    let { cancel, class: className, onselect, value = $bindable() }: Props = $props();
    const id = $props.id();

    // Simplified quick ranges — just the most commonly used
    const commonRanges = [
        {
            label: 'Last 15 minutes',
            value: '[now-15m TO now]'
        },
        {
            label: 'Last 1 hour',
            value: '[now-1h TO now]'
        },
        {
            label: 'Last 4 hours',
            value: '[now-4h TO now]'
        },
        {
            label: 'Last 24 hours',
            value: '[now-1d TO now]'
        },
        {
            label: 'Last 7 days',
            value: '[now-7d TO now]'
        },
        {
            label: 'Last 30 days',
            value: '[now-30d TO now]'
        },
        {
            label: 'Last 90 days',
            value: '[now-90d TO now]'
        }
    ];

    let showCustom = $state(false);
    let startValue = $state('');
    let endValue = $state('');

    // Keep custom fields synchronized with the persisted range. Common ranges stay collapsed,
    // but their values must still be available when Custom range is opened after a remount.
    $effect(() => {
        const range = typeof value === 'string' ? extractRangeExpressions(value) : null;
        if (range) {
            startValue = range.start ?? '';
            endValue = range.end ?? '';

            if (!commonRanges.some((r) => r.value === value)) {
                showCustom = true;
            }
        } else if (!value) {
            startValue = '';
            endValue = '';
            showCustom = false;
        }
    });

    const resolvedRange = $derived.by(() => {
        const referenceTime = new Date();
        return {
            end: parseDateMath(endValue, referenceTime, true),
            start: parseDateMath(startValue, referenceTime)
        };
    });
    const startValidation = $derived(resolvedRange.start);
    const endValidation = $derived(resolvedRange.end);
    const rangeError = $derived(
        startValidation.success && endValidation.success && startValidation.date > endValidation.date ? 'End must be on or after start.' : undefined
    );
    const isCustomValid = $derived(startValidation.success && endValidation.success && !rangeError);
    const inputError = 'Enter a year, month, date, timestamp, or relative time such as now-1h.';

    export function apply() {
        if (showCustom && isCustomValid) {
            applyCustom();
        }
    }

    function applyCustom() {
        if (isCustomValid) {
            const customValue = `[${startValue.trim()} TO ${endValue.trim()}]`;
            value = customValue;
            onselect?.(customValue);
        }
    }

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Enter' && isCustomValid) {
            event.preventDefault();
            applyCustom();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            cancel?.();
        }
    }

    function selectRange(rangeValue: string) {
        value = rangeValue;
        onselect?.(rangeValue);
    }
</script>

<div class={className}>
    <div class="flex flex-col p-1">
        {#each commonRanges as range (range.value)}
            <button
                type="button"
                class="hover:bg-muted hover:text-foreground flex items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden transition-colors select-none"
                onclick={() => selectRange(range.value)}
            >
                <span class="size-4 shrink-0">
                    {#if range.value === value}
                        <Check class="text-primary size-4" />
                    {/if}
                </span>
                <span class={range.value === value ? 'font-medium' : ''}>{range.label}</span>
            </button>
        {/each}

        <button
            type="button"
            class="hover:bg-muted hover:text-foreground flex items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden transition-colors select-none"
            aria-expanded={showCustom}
            aria-controls={`${id}-custom`}
            onclick={() => (showCustom = !showCustom)}
        >
            <ChevronRight class={['text-muted-foreground size-4 shrink-0 transition-transform', showCustom && 'rotate-90']} />
            <span class={showCustom ? 'font-medium' : ''}>Custom range</span>
        </button>

        {#if showCustom}
            <Field.FieldGroup id={`${id}-custom`} class="gap-3 px-2 pt-2 pb-1">
                <Field.FieldDescription id={`${id}-help`}>
                    Use 2024, 2024-01, or 2024-01-01. Start and end include the whole year, month, or day in local time.
                </Field.FieldDescription>
                <Field.Field class="gap-1" data-invalid={!!startValue && !startValidation.success}>
                    <Field.FieldLabel for={`${id}-start`}>Start</Field.FieldLabel>
                    <Input
                        id={`${id}-start`}
                        placeholder="e.g. 2024-01-01 or now-1h"
                        class="h-7 font-mono text-xs"
                        bind:value={startValue}
                        aria-invalid={startValue ? !startValidation.success : undefined}
                        aria-describedby={`${id}-help ${id}-start-status`}
                        onkeydown={handleKeyDown}
                    />
                    <div id={`${id}-start-status`} aria-live="polite">
                        {#if startValue && startValidation.success}
                            <p class="text-muted-foreground text-[11px]"><DateTime value={startValidation.date} /></p>
                        {:else if startValue}
                            <Field.FieldError>{inputError}</Field.FieldError>
                        {/if}
                    </div>
                </Field.Field>
                <Field.Field class="gap-1" data-invalid={(!!endValue && !endValidation.success) || !!rangeError}>
                    <Field.FieldLabel for={`${id}-end`}>End</Field.FieldLabel>
                    <Input
                        id={`${id}-end`}
                        placeholder="e.g. 2024-01-31 or now"
                        class="h-7 font-mono text-xs"
                        bind:value={endValue}
                        aria-invalid={endValue ? !endValidation.success || !!rangeError : undefined}
                        aria-describedby={`${id}-help ${id}-end-status`}
                        onkeydown={handleKeyDown}
                    />
                    <div id={`${id}-end-status`} aria-live="polite">
                        {#if endValue && endValidation.success}
                            <p class="text-muted-foreground text-[11px]"><DateTime value={endValidation.date} /></p>
                        {:else if endValue}
                            <Field.FieldError>{inputError}</Field.FieldError>
                        {/if}
                        {#if rangeError}
                            <Field.FieldError>{rangeError}</Field.FieldError>
                        {/if}
                    </div>
                </Field.Field>
                <Button size="sm" class="w-full" disabled={!isCustomValid} onclick={applyCustom}>Apply</Button>
            </Field.FieldGroup>
        {/if}
    </div>
</div>
