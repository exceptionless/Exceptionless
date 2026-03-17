<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { BillingPlan } from '$lib/generated/api';
    import type { Stripe, StripeElements } from '@stripe/stripe-js';

    import ErrorMessage from '$comp/error-message.svelte';
    import { H4, P } from '$comp/typography';
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
    import { createForm } from '@tanstack/svelte-form';
    import { toast } from 'svelte-sonner';
    import { PaymentElement } from 'svelte-stripe';

    interface Props {
        open: boolean;
        organization: ViewOrganization;
    }

    let { open = $bindable(), organization }: Props = $props();

    // Queries and mutations
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
                    const selectedPlan = plansQuery.data?.find((p: BillingPlan) => p.id === value.selectedPlanId) ?? null;
                    const isPaidPlan = !!selectedPlan && selectedPlan.price > 0;
                    const needsPayment = isPaidPlan && value.cardMode === 'new';

                    if (needsPayment) {
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

    // Derived: Default plan is next tier (upsell) or current if at top tier
    const defaultPlanId = $derived.by(() => {
        if (!plansQuery.data) {
            return undefined;
        }
        const currentPlanIndex = plansQuery.data.findIndex((p: BillingPlan) => p.id === organization.plan_id);
        const nextPlan = plansQuery.data[currentPlanIndex + 1] ?? plansQuery.data[currentPlanIndex];
        return nextPlan?.id;
    });

    // Derived state
    const selectedPlanId = $derived(form.state.values.selectedPlanId || defaultPlanId);
    const cardMode = $derived(form.state.values.cardMode);
    const selectedPlan = $derived(plansQuery.data?.find((p: BillingPlan) => p.id === selectedPlanId) ?? null);
    const isPaidPlan = $derived(selectedPlan && selectedPlan.price > 0);
    const isDowngradeToFree = $derived(selectedPlanId === FREE_PLAN_ID && organization.plan_id !== FREE_PLAN_ID);
    const needsPayment = $derived(isPaidPlan && cardMode === 'new');
    const isCurrentPlan = $derived(selectedPlanId === organization.plan_id);

    $effect(() => {
        if (open) {
            form.reset();
            form.setFieldValue('cardMode', hasExistingCard ? 'existing' : 'new');
            form.setFieldValue('couponId', '');
            form.setFieldValue('selectedPlanId', defaultPlanId ?? '');
        }
    });

    function handleCancel() {
        open = false;
    }

    function formatPrice(price: number): string {
        return price === 0 ? 'Free' : `$${price}/month`;
    }

    function formatEvents(events: number): string {
        if (events >= 1000000) {
            return `${(events / 1000000).toFixed(0)}M`;
        }
        if (events >= 1000) {
            return `${(events / 1000).toFixed(0)}K`;
        }
        return events.toString();
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="max-w-2xl">
        <Dialog.Header>
            <Dialog.Title class="flex items-center gap-2">
                <CreditCard class="size-5" />
                Change Plan
            </Dialog.Title>
            <Dialog.Description>
                You are currently on the <strong>{organization.plan_name}</strong> plan.
            </Dialog.Description>
        </Dialog.Header>

        {#if !isStripeEnabled()}
            <div class="py-4">
                <ErrorMessage message="Billing is currently disabled." />
            </div>
        {:else if plansQuery.isLoading}
            <div class="space-y-4 py-4">
                <Skeleton class="h-20 w-full" />
                <Skeleton class="h-20 w-full" />
                <Skeleton class="h-20 w-full" />
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

                <div class="space-y-6 py-4">
                    <!-- Plan Selection -->
                    <form.Field name="selectedPlanId">
                        {#snippet children(field)}
                            <Field.Field>
                                <Field.Label for={field.name}>Select a Plan</Field.Label>
                                <RadioGroup.Root
                                    value={field.state.value || defaultPlanId || ''}
                                    onValueChange={(value) => field.handleChange(value)}
                                    class="grid gap-3"
                                >
                                    {#each plansQuery.data as plan (plan.id)}
                                        <div
                                            class="border-border hover:border-primary has-data-[state=checked]:border-primary flex items-center gap-4 rounded-lg border p-4 transition-colors"
                                        >
                                            <RadioGroup.Item value={plan.id} id="plan-{plan.id}" />
                                            <Field.Label for="plan-{plan.id}" class="flex flex-1 cursor-pointer items-center justify-between">
                                                <div>
                                                    <div class="font-semibold">
                                                        {plan.name}
                                                        {#if plan.id === organization.plan_id}
                                                            <span class="text-muted-foreground ml-2 text-sm font-normal">(Current)</span>
                                                        {/if}
                                                    </div>
                                                    <div class="text-muted-foreground text-sm">{plan.description}</div>
                                                    <div class="text-muted-foreground mt-1 text-xs">
                                                        {formatEvents(plan.max_events_per_month)} events/mo • {plan.retention_days} days retention • {plan.max_projects}
                                                        projects • {plan.max_users} users
                                                    </div>
                                                </div>
                                                <div class="text-right">
                                                    <div class="text-lg font-bold">{formatPrice(plan.price)}</div>
                                                </div>
                                            </Field.Label>
                                        </div>
                                    {/each}
                                </RadioGroup.Root>
                                <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                            </Field.Field>
                        {/snippet}
                    </form.Field>

                    <!-- Payment Section (only for paid plans) -->
                    {#if isPaidPlan}
                        <Separator />

                        <div class="space-y-4">
                            <H4>Payment Method</H4>

                            <!-- Card mode selection (if has existing card) -->
                            {#if hasExistingCard}
                                <form.Field name="cardMode">
                                    {#snippet children(field)}
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
                                    {/snippet}
                                </form.Field>
                            {/if}

                            <!-- Stripe PaymentElement for new card -->
                            {#if needsPayment}
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
                                >
                                    <div class="rounded-lg border p-4">
                                        <PaymentElement />
                                    </div>
                                </StripeProvider>
                            {/if}

                            <!-- Coupon input (only for new customers or card changes) -->
                            {#if !hasExistingCard || cardMode === 'new'}
                                <form.Field name="couponId">
                                    {#snippet children(field)}
                                        <Field.Field>
                                            <Field.Label for={field.name}>Coupon Code (optional)</Field.Label>
                                            <Input
                                                id={field.name}
                                                name={field.name}
                                                type="text"
                                                placeholder="Enter coupon code"
                                                value={field.state.value}
                                                onblur={field.handleBlur}
                                                oninput={(e) => field.handleChange(e.currentTarget.value)}
                                            />
                                            <Field.Error errors={mapFieldErrors(field.state.meta.errors)} />
                                        </Field.Field>
                                    {/snippet}
                                </form.Field>
                            {/if}
                        </div>
                    {/if}

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
