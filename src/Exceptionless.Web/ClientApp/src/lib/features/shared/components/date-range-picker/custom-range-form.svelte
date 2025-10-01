<script lang="ts">
    import DateTime from '$comp/formatters/date-time.svelte';
    import { Button } from '$comp/ui/button';
    import * as Form from '$comp/ui/form';
    import { Input } from '$comp/ui/input';
    import * as Tooltip from '$comp/ui/tooltip/index.js';
    import { CustomDateRange } from '$features/shared/models';
    import { validateAndResolveTime, validateDateMath } from '$features/shared/utils/datemath';
    import { structuredCloneState } from '$features/shared/utils/state.svelte';
    import HelpCircle from '@lucide/svelte/icons/help-circle';
    import { defaults, superForm } from 'sveltekit-superforms';
    import { classvalidatorClient } from 'sveltekit-superforms/adapters';

    interface Props {
        apply?: (range: CustomDateRange) => void;
        cancel?: () => void;
        range?: CustomDateRange | null;
    }

    let { apply, cancel, range = null }: Props = $props();
    let previousRange: CustomDateRange | null | undefined;

    const initialData = structuredCloneState(range) || new CustomDateRange();

    const form = superForm(defaults(initialData, classvalidatorClient(CustomDateRange)), {
        dataType: 'json',
        id: 'custom-range-form',
        async onUpdate({ form }) {
            if (!form.valid) {
                return;
            }

            apply?.(form.data);
        },
        SPA: true,
        validators: false
    });

    const { enhance, form: formData } = form;

    // Reset form when range prop changes
    $effect(() => {
        if (range !== previousRange) {
            const clonedRange = structuredCloneState(range);
            form.reset({ data: clonedRange || new CustomDateRange(), keepMessage: true });
            previousRange = range;
        }
    });

    // Validation using datemath
    const startValidation = $derived(validateDateMath($formData.start || ''));
    const endValidation = $derived(validateDateMath($formData.end || ''));

    // Resolved time previews using datemath
    const startResolved = $derived(startValidation.valid ? validateAndResolveTime($formData.start || '') : null);
    const endResolved = $derived(endValidation.valid ? validateAndResolveTime($formData.end || '') : null);

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
        <form method="POST" use:enhance class="space-y-4">
            <Form.Field {form} name="start">
                <Form.Control>
                    {#snippet children({ props })}
                        <Form.Label>
                            Start
                            <Tooltip.Root>
                                <Tooltip.Trigger>
                                    <Button size="icon" variant="ghost" aria-label="Start examples">
                                        <HelpCircle />
                                    </Button>
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
                        </Form.Label>
                        <Input
                            {...props}
                            bind:value={$formData.start}
                            placeholder={startPlaceholder}
                            class="font-mono text-sm"
                            aria-invalid={!startValidation.valid}
                        />
                    {/snippet}
                </Form.Control>
                <Form.Description>
                    {#if startValidation.valid && startResolved}
                        <DateTime value={startResolved} />
                    {:else if startValidation.error}
                        <span class="text-destructive">{startValidation.error}</span>
                    {/if}
                </Form.Description>
            </Form.Field>

            <Form.Field {form} name="end">
                <Form.Control>
                    {#snippet children({ props })}
                        <Form.Label>
                            End
                            <Tooltip.Root>
                                <Tooltip.Trigger>
                                    <Button size="icon" variant="ghost" aria-label="End examples">
                                        <HelpCircle />
                                    </Button>
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
                        </Form.Label>
                        <Input
                            {...props}
                            bind:value={$formData.end}
                            placeholder={endPlaceholder}
                            class="font-mono text-sm"
                            aria-invalid={!endValidation.valid}
                        />
                    {/snippet}
                </Form.Control>
                <Form.Description>
                    {#if endValidation.valid && endResolved}
                        <DateTime value={endResolved} />
                    {:else if endValidation.error}
                        <span class="text-destructive">{endValidation.error}</span>
                    {/if}
                </Form.Description>
            </Form.Field>

            <div class="flex justify-between">
                <Button type="button" variant="outline" onclick={() => cancel?.()}>Cancel</Button>
                <Button type="submit" disabled={!startValidation.valid || !endValidation.valid}>Apply</Button>
            </div>
        </form>
    </Tooltip.Provider>
</div>
