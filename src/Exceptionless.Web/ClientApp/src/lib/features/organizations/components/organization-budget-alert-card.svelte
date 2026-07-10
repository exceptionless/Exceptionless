<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';

    import ErrorMessage from '$comp/error-message.svelte';
    import NumberFormatter from '$comp/formatters/number.svelte';
    import { Button } from '$comp/ui/button';
    import * as Card from '$comp/ui/card';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import { Spinner } from '$comp/ui/spinner';
    import { Switch } from '$comp/ui/switch';
    import { patchOrganization } from '$features/organizations/api.svelte';
    import { getBudgetThresholdEventCount, parseBudgetThresholds } from '$features/organizations/budget-utils';
    import { type BudgetAlertCardFormData, BudgetAlertCardSchema } from '$features/organizations/schemas';
    import { getEffectiveEventLimit } from '$features/organizations/utils';
    import { ariaInvalid, getFormErrorMessages, mapFieldErrors, problemDetailsToFormErrors } from '$features/shared/validation';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';

    let { organization }: { organization: ViewOrganization } = $props();

    const update = patchOrganization({
        route: {
            get id() {
                return organization.id;
            }
        }
    });
    const organizationLimit = $derived(getEffectiveEventLimit(organization));
    const isUnlimited = $derived(organizationLimit < 0);

    const form = createForm(() => ({
        defaultValues: {
            enabled: organization.budget_alert_settings?.enabled ?? false,
            thresholds: organization.budget_alert_settings?.thresholds.join(', ') ?? '50, 80'
        } as BudgetAlertCardFormData,
        validators: {
            onSubmit: BudgetAlertCardSchema,
            onSubmitAsync: async ({ value }) => {
                if (value.enabled && isUnlimited) {
                    return { form: 'Budget alerts require a finite monthly event allowance.' };
                }

                const thresholds = parseBudgetThresholds(value.thresholds);

                try {
                    await update.mutateAsync({ budget_alert_settings: { enabled: value.enabled, thresholds } });
                    toast.success('Budget alert settings saved.');
                    return null;
                } catch (error: unknown) {
                    toast.error('Unable to save budget alert settings.');
                    return error instanceof ProblemDetails ? problemDetailsToFormErrors(error) : { form: 'An unexpected error occurred.' };
                }
            }
        }
    }));

    async function clearSettings() {
        try {
            await update.mutateAsync({ budget_alert_settings: null });
            form.setFieldValue('enabled', false);
            form.setFieldValue('thresholds', '50, 80');
            toast.success('Budget alert settings cleared.');
        } catch {
            toast.error('Unable to clear budget alert settings.');
        }
    }
</script>

<Card.Root>
    <Card.Header>
        <Card.Title>Budget alerts</Card.Title>
        <Card.Description>Receive an email when accepted organization usage crosses selected percentages of the monthly allowance.</Card.Description>
    </Card.Header>
    <form
        onsubmit={(event) => {
            event.preventDefault();
            event.stopPropagation();
            form.handleSubmit();
        }}
    >
        <Card.Content>
            <form.Subscribe selector={(state) => state.errors}>
                {#snippet children(errors)}
                    <ErrorMessage message={getFormErrorMessages(errors)} />
                {/snippet}
            </form.Subscribe>

            <Field.Group>
                <form.Field name="enabled">
                    {#snippet children(field)}
                        <Field.Field orientation="horizontal">
                            <Field.Content>
                                <Field.Label for={field.name}>Enable budget alerts</Field.Label>
                                <Field.Description>Alerts are sent once per threshold per monthly usage period.</Field.Description>
                            </Field.Content>
                            <Switch
                                id={field.name}
                                checked={field.state.value}
                                onCheckedChange={(checked) => field.handleChange(checked)}
                                disabled={update.isPending || isUnlimited}
                            />
                        </Field.Field>
                    {/snippet}
                </form.Field>

                <form.Field name="thresholds">
                    {#snippet children(field)}
                        <Field.Field data-invalid={ariaInvalid(field)}>
                            <Field.Label for={field.name}>Threshold percentages</Field.Label>
                            <Input
                                id={field.name}
                                name={field.name}
                                inputmode="numeric"
                                placeholder="50, 80, 90"
                                value={field.state.value}
                                onblur={field.handleBlur}
                                oninput={(event) => field.handleChange(event.currentTarget.value)}
                                aria-invalid={ariaInvalid(field)}
                                aria-describedby="budget-threshold-help"
                                disabled={update.isPending}
                            />
                            <Field.Description id="budget-threshold-help">Comma-separated whole numbers from 1 to 99.</Field.Description>
                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />

                            <p class="text-muted-foreground text-sm" aria-live="polite">
                                Current allowance:
                                {#if isUnlimited}
                                    unlimited; percentage alerts are inactive.
                                {:else}
                                    <NumberFormatter value={organizationLimit} /> events.
                                {/if}
                            </p>
                            {#if !isUnlimited}
                                <ul class="text-muted-foreground grid gap-1 text-sm" aria-live="polite">
                                    {#each parseBudgetThresholds(field.state.value).filter((threshold) => Number.isInteger(threshold) && threshold >= 1 && threshold <= 99) as threshold (threshold)}
                                        <li>
                                            {threshold}% =
                                            <NumberFormatter value={getBudgetThresholdEventCount(organizationLimit, threshold)!} /> events
                                        </li>
                                    {/each}
                                </ul>
                            {/if}
                        </Field.Field>
                    {/snippet}
                </form.Field>
            </Field.Group>
        </Card.Content>
        <Card.Footer class="justify-between gap-3">
            <Button type="button" variant="ghost" onclick={clearSettings} disabled={update.isPending || !organization.budget_alert_settings}
                >Clear settings</Button
            >
            <form.Subscribe selector={(state) => state.isSubmitting}>
                {#snippet children(isSubmitting)}
                    <Button type="submit" disabled={isSubmitting || update.isPending}>
                        {#if isSubmitting || update.isPending}<Spinner />{/if}
                        Save budget alerts
                    </Button>
                {/snippet}
            </form.Subscribe>
        </Card.Footer>
    </form>
</Card.Root>
