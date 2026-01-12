<script lang="ts">
    import type { CustomDateRange } from '$features/shared/models';

    import DateTime from '$comp/formatters/date-time.svelte';
    import { Button } from '$comp/ui/button';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as Tooltip from '$comp/ui/tooltip/index.js';
    import { CustomDateRangeSchema } from '$features/shared/schemas';
    import { validateAndResolveTime, validateDateMath } from '$features/shared/utils/datemath';
    import { structuredCloneState } from '$features/shared/utils/state.svelte';
    import HelpCircle from '@lucide/svelte/icons/help-circle';
    import { createForm } from '@tanstack/svelte-form';

    interface Props {
        apply?: (range: CustomDateRange) => void;
        cancel?: () => void;
        range?: CustomDateRange | null;
    }

    let { apply, cancel, range = null }: Props = $props();
    let previousRange: CustomDateRange | null | undefined;

    const form = createForm(() => ({
        defaultValues: structuredCloneState(range) ?? { end: '', start: '' },
        validators: {
            onSubmit: CustomDateRangeSchema,
            onSubmitAsync: async ({ value }) => {
                apply?.(value);
                return null;
            }
        }
    }));

    // TODO: See if we can simplify this component state
    // Track form values for validation display (updated via oninput handlers)
    let startValue = $state('');
    let endValue = $state('');

    // Initialize values on mount and reset when range prop changes
    $effect(() => {
        if (range !== previousRange) {
            const clonedRange = structuredCloneState(range) ?? { end: '', start: '' };
            form.reset();
            form.setFieldValue('start', clonedRange.start ?? '');
            form.setFieldValue('end', clonedRange.end ?? '');
            startValue = clonedRange.start ?? '';
            endValue = clonedRange.end ?? '';
            previousRange = range;
        }
    });

    // Validation using datemath
    // TODO: See if we can move this into the zod schema directly.
    const startValidation = $derived(validateDateMath(startValue));
    const startResolved = $derived(startValidation.valid ? validateAndResolveTime(startValue) : null);

    const endValidation = $derived.by(() => {
        const basicValidation = validateDateMath(endValue);
        if (!basicValidation.valid) {
            return basicValidation;
        }

        const endTime = validateAndResolveTime(endValue);
        if (startResolved && endTime && startResolved > endTime) {
            return { error: 'End date must be after start date', valid: false };
        }

        return basicValidation;
    });

    const endResolved = $derived(endValidation.valid ? validateAndResolveTime(endValue) : null);
    const isValid = $derived(startValidation.valid && endValidation.valid && startValue && endValue);

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Enter' && !cancel && isValid) {
            event.preventDefault();
            form.handleSubmit();
        } else if (event.key === 'Escape') {
            event.preventDefault();
            cancel?.();
        }
    }

    export function submitIfValid() {
        if (!cancel && isValid) {
            form.handleSubmit();
        }
    }

    // Date examples using current date with reasonable example times
    const currentDateString = new Date().toISOString().split('T')[0]; // YYYY-MM-DD format
    const isoExample = `${currentDateString}T10:00:00Z`;
    const localExample = `${currentDateString}T10:00:00`;

    // Placeholders using today's date with example times
    const startPlaceholder = `e.g., now-2h, ${currentDateString}T10:00:00, now-1d`;
    const endPlaceholder = `e.g., now, ${currentDateString}T12:00:00`;
</script>

<div class="space-y-4">
    <Tooltip.Provider delayDuration={0}>
        <form
            class="space-y-4"
            onsubmit={(e) => {
                e.preventDefault();
                e.stopPropagation();
                form.handleSubmit();
            }}
        >
            <form.Field name="start">
                {#snippet children(field)}
                    <Field.Field data-invalid={!startValidation.valid ? true : undefined}>
                        <Field.Label for={field.name}>
                            Start
                            <Tooltip.Root>
                                <Tooltip.Trigger>
                                    {#snippet child({ props })}
                                        <Button {...props} size="icon" variant="ghost" aria-label="Start examples">
                                            <HelpCircle />
                                        </Button>
                                    {/snippet}
                                </Tooltip.Trigger>
                                <Tooltip.Content side="top" align="start">
                                    <div class="max-w-sm">
                                        <p class="font-medium">Use relative (now-1h) or absolute timestamps</p>
                                        <ul class="mt-1 list-inside list-disc space-y-0.5 font-mono text-xs">
                                            <li>now-2h (2 hours ago)</li>
                                            <li>now-30m (30 minutes ago)</li>
                                            <li>now/d (start of today)</li>
                                            <li>{isoExample} (ISO datetime)</li>
                                            <li>{localExample} (local datetime)</li>
                                        </ul>
                                    </div>
                                </Tooltip.Content>
                            </Tooltip.Root>
                        </Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            value={field.state.value ?? ''}
                            placeholder={startPlaceholder}
                            class="font-mono text-sm"
                            aria-invalid={!startValidation.valid}
                            aria-describedby="custom-range-form-help"
                            onblur={field.handleBlur}
                            oninput={(e) => {
                                const value = e.currentTarget.value;
                                field.handleChange(value);
                                startValue = value;
                            }}
                            onkeydown={handleKeyDown}
                        />
                        <Field.Description>
                            {#if startValidation.valid && startResolved}
                                <DateTime value={startResolved} />
                            {:else if startValidation.error}
                                <span class="text-destructive">{startValidation.error}</span>
                            {/if}
                        </Field.Description>
                    </Field.Field>
                {/snippet}
            </form.Field>

            <form.Field name="end">
                {#snippet children(field)}
                    <Field.Field data-invalid={!endValidation.valid ? true : undefined}>
                        <Field.Label for={field.name}>
                            End
                            <Tooltip.Root>
                                <Tooltip.Trigger>
                                    {#snippet child({ props })}
                                        <Button {...props} size="icon" variant="ghost" aria-label="End examples">
                                            <HelpCircle />
                                        </Button>
                                    {/snippet}
                                </Tooltip.Trigger>
                                <Tooltip.Content side="top" align="start">
                                    <div class="max-w-sm">
                                        <p class="font-medium">Use 'now' for current time or specific timestamp</p>
                                        <ul class="mt-1 list-inside list-disc space-y-0.5 font-mono text-xs">
                                            <li>now (current time)</li>
                                            <li>now-1h (1 hour ago)</li>
                                            <li>now/d+1d (end of today)</li>
                                            <li>{isoExample} (ISO datetime)</li>
                                            <li>{localExample} (local datetime)</li>
                                        </ul>
                                    </div>
                                </Tooltip.Content>
                            </Tooltip.Root>
                        </Field.Label>
                        <Input
                            id={field.name}
                            name={field.name}
                            value={field.state.value ?? ''}
                            placeholder={endPlaceholder}
                            class="font-mono text-sm"
                            aria-invalid={!endValidation.valid}
                            aria-describedby="custom-range-form-help"
                            onblur={field.handleBlur}
                            oninput={(e) => {
                                const value = e.currentTarget.value;
                                field.handleChange(value);
                                endValue = value;
                            }}
                            onkeydown={handleKeyDown}
                        />
                        <Field.Description>
                            {#if endValidation.valid && endResolved}
                                <DateTime value={endResolved} />
                            {:else if endValidation.error}
                                <span class="text-destructive">{endValidation.error}</span>
                            {/if}
                        </Field.Description>
                    </Field.Field>
                {/snippet}
            </form.Field>

            {#if cancel}
                <div class="flex justify-between">
                    <Button type="button" variant="outline" onclick={() => cancel?.()}>Cancel</Button>
                    <Button type="submit" disabled={!isValid}>Apply</Button>
                </div>
            {/if}
        </form>
        <div id="custom-range-form-help" class="sr-only">Press Enter to apply filter, Escape to cancel</div>
    </Tooltip.Provider>
</div>
