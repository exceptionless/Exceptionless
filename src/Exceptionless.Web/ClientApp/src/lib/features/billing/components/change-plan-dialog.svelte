<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { BillingPlan } from '$lib/generated/api';
    import type { Stripe, StripeElements } from '@stripe/stripe-js';

    import ErrorMessage from '$comp/error-message.svelte';
    import { P } from '$comp/typography';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import * as Field from '$comp/ui/field';
    import { Input } from '$comp/ui/input';
    import * as RadioGroup from '$comp/ui/radio-group';
    import { Separator } from '$comp/ui/separator';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Spinner } from '$comp/ui/spinner';
    import { FREE_PLAN_ID, isStripeEnabled, StripeProvider } from '$features/billing';
    import { type ChangePlanFormData, ChangePlanSchema } from '$features/billing/schemas';
    import { changePlanMutation, getPlansQuery } from '$features/organizations/api.svelte';
    import { getFormErrorMessages, mapFieldErrors } from '$features/shared/validation';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import ExternalLink from '@lucide/svelte/icons/external-link';
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';
    import { untrack } from 'svelte';

    interface Props {
        open: boolean;
        organization: ViewOrganization;
    }

    let { open = $bindable(), organization }: Props = $props();

    const plansQuery = getPlansQuery({
        route: {
            get organizationId() {
                return organization.id;
            }
        }
    });

    const changePlan = changePlanMutation({
        route: {
            get organizationId() {
                return organization.id;
            }
        }
    });

    const hasExistingCard = $derived(!!organization.card_last4);
    let stripe = $state<null | Stripe>(null);
    let stripeElements = $state<StripeElements | undefined>(undefined);

    const form = createForm(() => ({
        defaultValues: {
            cardMode: hasExistingCard ? 'existing' : 'new',
            couponId: '',
            selectedPlanId: ''
        } as ChangePlanFormData,
        validators: {
            onSubmit: ChangePlanSchema,
            onSubmitAsync: async ({ value }) => {
                try {
                    let stripeToken: string | undefined;
                    let last4: string | undefined;
                    const plan = plansQuery.data?.find((p: BillingPlan) => p.id === value.selectedPlanId) ?? null;
                    const isPaid = !!plan && plan.price > 0;
                    const needsCard = isPaid && value.cardMode === 'new';

                    if (needsCard) {
                        if (!stripe || !stripeElements) {
                            return { form: 'Payment system not loaded. Please try again.' };
                        }

                        const { error: submitError } = await stripeElements.submit();
                        if (submitError) {
                            return { form: submitError.message ?? 'Payment validation failed' };
                        }

                        const { error: pmError, paymentMethod } = await stripe.createPaymentMethod({
                            elements: stripeElements
                        });

                        if (pmError) {
                            return { form: pmError.message ?? 'Failed to process payment method' };
                        }

                        stripeToken = paymentMethod.id;
                        last4 = paymentMethod.card?.last4;
                    }

                    const result = await changePlan.mutateAsync({
                        couponId: value.couponId || undefined,
                        last4,
                        planId: value.selectedPlanId,
                        stripeToken
                    });

                    if (!result.success) {
                        return { form: result.message ?? 'Failed to change plan' };
                    }

                    toast.success(result.message ?? 'Your billing plan has been successfully changed.');
                    open = false;
                    return null;
                } catch (error: unknown) {
                    return { form: error instanceof Error ? error.message : 'An unexpected error occurred' };
                }
            }
        }
    }));

    // Default to the next tier up (upsell) or current plan if at top
    const defaultPlanId = $derived.by(() => {
        if (!plansQuery.data) return undefined;
        const idx = plansQuery.data.findIndex((p: BillingPlan) => p.id === organization.plan_id);
        return (plansQuery.data[idx + 1] ?? plansQuery.data[idx])?.id;
    });

    let selectedPlanId = $state('');
    const selectedPlan = $derived(plansQuery.data?.find((p: BillingPlan) => p.id === selectedPlanId) ?? null);
    const isPaidPlan = $derived(selectedPlan && selectedPlan.price > 0);
    const isDowngradeToFree = $derived(selectedPlanId === FREE_PLAN_ID && organization.plan_id !== FREE_PLAN_ID);
    const cardMode = $derived(form.state.values.cardMode);
    const needsPayment = $derived(isPaidPlan && cardMode === 'new');
    const isCurrentPlan = $derived(selectedPlanId === organization.plan_id);

    // Reset form when dialog opens — untrack form mutations to prevent reactive cycles
    $effect(() => {
        if (open && defaultPlanId) {
            untrack(() => {
                form.reset();
                form.setFieldValue('cardMode', hasExistingCard ? 'existing' : 'new');
                form.setFieldValue('couponId', '');
                form.setFieldValue('selectedPlanId', defaultPlanId);
            });
            selectedPlanId = defaultPlanId;
        }
    });

    function handlePlanChange(e: Event & { currentTarget: HTMLSelectElement }) {
        const value = e.currentTarget.value;
        selectedPlanId = value;
        untrack(() => form.setFieldValue('selectedPlanId', value));
    }

    function handleCancel() {
        open = false;
    }

    function formatPlanLabel(plan: BillingPlan): string {
        return plan.price === 0 ? `${plan.name} (Free)` : `${plan.name} ($${plan.price}/month)`;
    }

    function formatPrice(price: number): string {
        return price === 0 ? 'Free' : `$${price}/month`;
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="max-w-md">
        <Dialog.Header>
            <Dialog.Title class="flex items-center gap-2">
                <CreditCard class="size-5" />
                Change Plan
            </Dialog.Title>
        </Dialog.Header>

        {#if !isStripeEnabled()}
            <div class="py-4">
                <ErrorMessage message="Billing is currently disabled." />
            </div>
        {:else if plansQuery.isLoading}
            <div class="space-y-3 py-4">
                <Skeleton class="h-5 w-3/4" />
                <Skeleton class="h-10 w-full" />
            </div>
        {:else if plansQuery.error}
            <div class="py-4">
                <ErrorMessage message="Failed to load available plans. Please try again." />
            </div>
        {:else if plansQuery.data}
            <form
                onsubmit={(e) => {
                    e.preventDefault();
                    form.handleSubmit();
                }}
            >
                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                    {/snippet}
                </form.Subscribe>

                <div class="space-y-4 py-2">
                    <!-- Current plan context -->
                    <P>
                        <strong>{organization.name}</strong> is currently on the <strong>{organization.plan_name}</strong> plan.
                    </P>

                    <!-- Plan dropdown -->
                    <form.Field name="selectedPlanId">
                        {#snippet children(field)}
                            <Field.Field>
                                <Field.Label for={field.name}>Select new plan</Field.Label>
                                <p class="text-muted-foreground text-xs">
                                    <a href="https://exceptionless.com/pricing" target="_blank" rel="noopener noreferrer" class="text-primary hover:underline inline-flex items-center gap-0.5">
                                        View plan details<ExternalLink class="inline size-3" />
                                    </a>
                                    &middot; All plan changes are prorated.
                                </p>
                                <select
                                    id={field.name}
                                    class="border-input bg-background ring-offset-background focus-visible:ring-ring flex h-9 w-full rounded-md border px-3 py-2 text-sm shadow-xs focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50"
                                    value={selectedPlanId}
                                    onchange={handlePlanChange}
                                    onblur={field.handleBlur}
                                >
                                    {#each plansQuery.data as plan (plan.id)}
                                        <option value={plan.id}>
                                            {formatPlanLabel(plan)}
                                        </option>
                                    {/each}
                                </select>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <!-- Plan comparison hint -->
                    {#if selectedPlan && !isCurrentPlan}
                        <div class="bg-muted/50 text-muted-foreground rounded-md px-3 py-2 text-sm">
                            {#if selectedPlan.price > 0}
                                Changing to <strong>{selectedPlan.name}</strong> at <strong>{formatPrice(selectedPlan.price)}</strong>
                            {:else}
                                Downgrading to the <strong>Free</strong> plan
                            {/if}
                        </div>
                    {/if}

                    <!-- Payment Section (only for paid plans) -->
                    {#if isPaidPlan}
                        <Separator />

                        <div class="space-y-4">
                            {#if hasExistingCard}
                                <form.Field name="cardMode">
                                    {#snippet children(field)}
                                        <Field.Field>
                                            <Field.Label>Payment Method</Field.Label>
                                            <RadioGroup.Root
                                                value={field.state.value}
                                                onValueChange={(value) => field.handleChange(value as 'existing' | 'new')}
                                                class="flex gap-6"
                                            >
                                                <div class="flex items-center gap-2">
                                                    <RadioGroup.Item value="existing" id="card-existing" />
                                                    <Field.Label for="card-existing">Card ending in {organization.card_last4}</Field.Label>
                                                </div>
                                                <div class="flex items-center gap-2">
                                                    <RadioGroup.Item value="new" id="card-new" />
                                                    <Field.Label for="card-new">Use a new card</Field.Label>
                                                </div>
                                            </RadioGroup.Root>
                                        </Field.Field>
                                    {/snippet}
                                </form.Field>
                            {/if}

                            {#if needsPayment}
                                <div class="rounded-lg border p-4">
                                    <StripeProvider
                                        appearance={{
                                            theme: 'stripe',
                                            variables: {
                                                borderRadius: '6px'
                                            }
                                        }}
                                        onElementsChange={(elements) => {
                                            stripeElements = elements;
                                        }}
                                        onload={(loadedStripe) => {
                                            stripe = loadedStripe;
                                        }}
                                    />
                                </div>
                            {/if}
                        </div>
                    {/if}

                    <!-- Coupon code -->
                    <form.Field name="couponId">
                        {#snippet children(field)}
                            <Field.Field>
                                <Field.Label for={field.name}>Coupon code</Field.Label>
                                <Input
                                    id={field.name}
                                    name={field.name}
                                    type="text"
                                    placeholder="Coupon code"
                                    value={field.state.value}
                                    onblur={field.handleBlur}
                                    oninput={(e) => field.handleChange(e.currentTarget.value)}
                                />
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <!-- Downgrade message -->
                    {#if isDowngradeToFree}
                        <div class="bg-muted rounded-lg p-4">
                            <P class="font-semibold">Help us improve Exceptionless!</P>
                            <P class="text-muted-foreground text-sm">
                                We hate to see you downgrade, but we'd love to hear your feedback. Please let us know why you're downgrading so we can serve you
                                better in the future.
                            </P>
                        </div>
                    {/if}
                </div>

                <form.Subscribe selector={(state) => state.isSubmitting}>
                    {#snippet children(isSubmitting)}
                        <Dialog.Footer>
                            <Button type="button" variant="outline" onclick={handleCancel} disabled={isSubmitting}>Cancel</Button>
                            <Button type="submit" disabled={isSubmitting || !selectedPlanId || isCurrentPlan}>
                                {#if isSubmitting}
                                    <Spinner class="mr-2" />
                                    Changing Plan...
                                {:else}
                                    Change Plan
                                {/if}
                            </Button>
                        </Dialog.Footer>
                    {/snippet}
                </form.Subscribe>
            </form>
        {/if}
    </Dialog.Content>
</Dialog.Root>
