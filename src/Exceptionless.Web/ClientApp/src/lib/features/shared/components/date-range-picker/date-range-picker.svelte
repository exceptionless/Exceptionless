<script lang="ts">
    import type { CustomDateRange } from '$features/shared/models';

    import DateTime from '$comp/formatters/date-time.svelte';
    import { Button } from '$comp/ui/button';
    import { Input } from '$comp/ui/input';
    import { extractRangeExpressions, validateAndResolveTime, validateDateMath } from '$features/shared/utils/datemath';
    import Check from '@lucide/svelte/icons/check';
    import ChevronRight from '@lucide/svelte/icons/chevron-right';

    type Props = {
        cancel?: () => void;
        class?: string;
        onselect?: (value: string) => void;
        value?: Date | string;
    };

    let { cancel, class: className, onselect, value = $bindable() }: Props = $props();

    // Simplified quick ranges — just the most commonly used
    const commonRanges = [
        { label: 'Last 15 minutes', value: '[now-15m TO now]' },
        { label: 'Last 1 hour', value: '[now-1h TO now]' },
        { label: 'Last 4 hours', value: '[now-4h TO now]' },
        { label: 'Last 24 hours', value: '[now-1d TO now]' },
        { label: 'Last 7 days', value: '[now-7d TO now]' },
        { label: 'Last 30 days', value: '[now-30d TO now]' },
        { label: 'Last 90 days', value: '[now-90d TO now]' }
    ];

    let showCustom = $state(false);
    let startValue = $state('');
    let endValue = $state('');

    // Initialize custom fields from current value if it's not a common range
    $effect(() => {
        const isCommon = commonRanges.some((r) => r.value === value);
        if (!isCommon && value && typeof value === 'string') {
            const range = extractRangeExpressions(value) as CustomDateRange | null;
            if (range) {
                startValue = range.start ?? '';
                endValue = range.end ?? '';
                showCustom = true;
            }
        }
    });

    const startValidation = $derived(validateDateMath(startValue));
    const startResolved = $derived(startValidation.valid ? validateAndResolveTime(startValue) : null);
    const endValidation = $derived(validateDateMath(endValue));
    const endResolved = $derived(endValidation.valid ? validateAndResolveTime(endValue) : null);
    const isCustomValid = $derived(startValidation.valid && endValidation.valid && !!startValue && !!endValue);

    function selectRange(rangeValue: string) {
        value = rangeValue;
        onselect?.(rangeValue);
    }

    function applyCustom() {
        if (isCustomValid) {
            const customValue = `[${startValue} TO ${endValue}]`;
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

    export function apply() {
        if (showCustom && isCustomValid) {
            applyCustom();
        }
    }
</script>

<div class={className}>
    <div class="flex flex-col p-1">
        {#each commonRanges as range (range.value)}
            <button
                type="button"
                class="flex items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden transition-colors select-none hover:bg-muted hover:text-foreground"
                onclick={() => selectRange(range.value)}
            >
                <span class="size-4 shrink-0">
                    {#if range.value === value}
                        <Check class="size-4 text-primary" />
                    {/if}
                </span>
                <span class={range.value === value ? 'font-medium' : ''}>{range.label}</span>
            </button>
        {/each}

        <button
            type="button"
            class="flex items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden transition-colors select-none hover:bg-muted hover:text-foreground"
            onclick={() => (showCustom = !showCustom)}
        >
            <ChevronRight class={['size-4 shrink-0 text-muted-foreground transition-transform', showCustom && 'rotate-90']} />
            <span class={showCustom ? 'font-medium' : ''}>Custom range</span>
        </button>

        {#if showCustom}
            <div class="space-y-2 px-2 pt-2 pb-1">
                <div>
                    <Input
                        placeholder="Start: now-1h, 2024-01-01"
                        class="h-7 font-mono text-xs"
                        bind:value={startValue}
                        aria-invalid={startValue ? !startValidation.valid : undefined}
                        onkeydown={handleKeyDown}
                    />
                    {#if startValue && startValidation.valid && startResolved}
                        <p class="mt-0.5 text-[11px] text-muted-foreground"><DateTime value={startResolved} /></p>
                    {:else if startValue && !startValidation.valid}
                        <p class="mt-0.5 text-[11px] text-destructive">{startValidation.error}</p>
                    {/if}
                </div>
                <div>
                    <Input
                        placeholder="End: now, 2024-12-31"
                        class="h-7 font-mono text-xs"
                        bind:value={endValue}
                        aria-invalid={endValue ? !endValidation.valid : undefined}
                        onkeydown={handleKeyDown}
                    />
                    {#if endValue && endValidation.valid && endResolved}
                        <p class="mt-0.5 text-[11px] text-muted-foreground"><DateTime value={endResolved} /></p>
                    {:else if endValue && !endValidation.valid}
                        <p class="mt-0.5 text-[11px] text-destructive">{endValidation.error}</p>
                    {/if}
                </div>
                <Button size="sm" class="w-full" disabled={!isCustomValid} onclick={applyCustom}>Apply</Button>
            </div>
        {/if}
    </div>
</div>
