<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { BillingPlan } from '$lib/generated/api';
    import type { Stripe, StripeElements } from '@stripe/stripe-js';

    import ErrorMessage from '$comp/error-message.svelte';
    import Currency from '$comp/formatters/currency.svelte';
    import NumberCompact from '$comp/formatters/number-compact.svelte';
    import { Muted, Small } from '$comp/typography';
    import * as Alert from '$comp/ui/alert';
    import { Badge } from '$comp/ui/badge';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Spinner } from '$comp/ui/spinner';
    import * as Tabs from '$comp/ui/tabs';
    import { FREE_PLAN_ID, isStripeEnabled, StripeProvider } from '$features/billing';
    import { type ChangePlanFormData, ChangePlanSchema } from '$features/billing/schemas';
    import { changePlanMutation, getPlansQuery } from '$features/organizations/api.svelte';
    import { getFormErrorMessages, problemDetailsToFormErrors } from '$features/shared/validation';
    import { Exceptionless } from '@exceptionless/browser';
    import { ProblemDetails } from '@exceptionless/fetchclient';
    import Check from '@lucide/svelte/icons/check';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import Plus from '@lucide/svelte/icons/plus';
    import { createForm } from '@tanstack/svelte-form';
    import { untrack } from 'svelte';
    import { toast } from 'svelte-sonner';
    import { SvelteMap } from 'svelte/reactivity';

    interface Props {
        initialCouponCode?: string;
        initialCouponOpen?: boolean;
        initialFormError?: string;
        onclose: (success: boolean) => void;
        organization: ViewOrganization;
    }

    let { initialCouponCode, initialCouponOpen, initialFormError, onclose, organization }: Props = $props();

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

    const YEARLY_SUFFIX = '_YEARLY';
    const POPULAR_TIER = 'EX_XL';

    function tierOf(planId: string): string {
        return planId.endsWith(YEARLY_SUFFIX) ? planId.slice(0, -YEARLY_SUFFIX.length) : planId;
    }

    function intervalOf(planId: string): 'month' | 'year' {
        return planId.endsWith(YEARLY_SUFFIX) ? 'year' : 'month';
    }

    interface PlanTier {
        id: string;
        monthly: BillingPlan | null;
        name: string;
        popular: boolean;
        yearly: BillingPlan | null;
    }

    function shouldIncludeInTiers(plan: BillingPlan): boolean {
        if (plan.is_hidden) {
            return false;
        }

        return plan.id !== FREE_PLAN_ID;
    }

    const tiers = $derived.by<PlanTier[]>(() => {
        if (!plansQuery.data) {
            return [];
        }

        const byTier = new SvelteMap<string, PlanTier>();

        for (const plan of plansQuery.data) {
            if (!shouldIncludeInTiers(plan)) {
                continue;
            }

            const key = tierOf(plan.id);
            const current = byTier.get(key) ?? {
                id: key,
                monthly: null,
                name: plan.name.replace(/\s*\(Yearly\)\s*$/i, '').trim(),
                popular: key === POPULAR_TIER,
                yearly: null
            };

            if (intervalOf(plan.id) === 'year') {
                current.yearly = plan;
            } else {
                current.monthly = plan;
            }

            byTier.set(key, current);
        }

        const ordered: PlanTier[] = [];
        for (const plan of plansQuery.data) {
            if (!shouldIncludeInTiers(plan)) {
                continue;
            }

            const key = tierOf(plan.id);
            const tier = byTier.get(key);
            if (tier && !ordered.includes(tier)) {
                ordered.push(tier);
            }
        }

        return ordered;
    });

    const freePlan = $derived(plansQuery.data?.find((plan: BillingPlan) => plan.id === FREE_PLAN_ID) ?? null);
    const isFreeCurrent = $derived(organization.plan_id === FREE_PLAN_ID);
    const currentInterval = $derived<'month' | 'year'>(intervalOf(organization.plan_id));
    const currentTierId = $derived(tierOf(organization.plan_id));
    const currentTierIndex = $derived(tiers.findIndex((t) => t.id === currentTierId));

    let selectedTierId = $state<string>('');
    let interval = $state<'month' | 'year'>('month');
    let paymentExpanded = $state(false);
    let couponOpen = $state(false);
    let couponApplied = $state<null | string>(null);
    let couponInput = $state('');
    let couponError = $state<null | string>(null);
    let couponInputEl = $state<HTMLInputElement | null>(null);
    let couponSectionEl = $state<HTMLElement | null>(null);
    let paymentSectionEl = $state<HTMLElement | null>(null);

    const hasExistingCard = $derived(!!organization.card_last4);

    function resolvePlanId(tierId: string, billingInterval: 'month' | 'year'): string {
        if (!tierId) {
            return FREE_PLAN_ID;
        }

        const tier = tiers.find((t) => t.id === tierId);
        if (!tier) {
            return FREE_PLAN_ID;
        }

        if (billingInterval === 'year' && tier.yearly) {
            return tier.yearly.id;
        }

        return tier.monthly?.id ?? tier.yearly?.id ?? FREE_PLAN_ID;
    }

    const selectedPlanId = $derived(resolvePlanId(selectedTierId, interval));
    const selectedPlan = $derived(plansQuery.data?.find((plan: BillingPlan) => plan.id === selectedPlanId) ?? null);
    const isFreeSelected = $derived(selectedPlanId === FREE_PLAN_ID);
    const isPaidPlan = $derived(!!selectedPlan && selectedPlan.price > 0);
    const isCurrentPlan = $derived(selectedPlanId === organization.plan_id);

    const planDirty = $derived(!isCurrentPlan);
    const paymentDirty = $derived(paymentExpanded && hasExistingCard);
    const needsPayment = $derived(isPaidPlan && (!hasExistingCard || paymentExpanded));
    const couponDirty = $derived(!!couponApplied);
    const anyDirty = $derived(planDirty || paymentDirty || couponDirty);

    const isDowngrade = $derived.by(() => {
        if (!planDirty) {
            return false;
        }

        if (isFreeSelected) {
            return true;
        }

        const selectedIdx = tiers.findIndex((t) => t.id === selectedTierId);
        return selectedIdx >= 0 && currentTierIndex >= 0 && selectedIdx < currentTierIndex;
    });

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

                    if (needsPayment && isPaidPlan) {
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
                        coupon_id: value.couponId || undefined,
                        last4,
                        plan_id: value.selectedPlanId,
                        stripe_token: stripeToken
                    });

                    if (!result.success) {
                        const message = result.message ?? 'Failed to change plan';
                        const isCouponError = /coupon/i.test(message);

                        if (isCouponError && value.couponId) {
                            // Clear applied coupon, reopen input with the bad code, focus it
                            couponApplied = null;
                            couponInput = value.couponId;
                            couponOpen = true;
                            couponError = message;
                            // Scroll coupon into view and focus the input after DOM updates
                            requestAnimationFrame(() => {
                                couponSectionEl?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                                couponInputEl?.focus();
                            });
                        }

                        toast.error(message);
                        return { form: message };
                    }

                    toast.success(result.message ?? 'Your billing plan has been successfully changed.');
                    onclose(true);

                    return null;
                    // TODO: Extract a shared error handler utility (toast + Exceptionless reporting)
                    //       to reduce boilerplate across mutation catch blocks.
                } catch (error: unknown) {
                    if (error instanceof ProblemDetails) {
                        const formErrors = problemDetailsToFormErrors(error);
                        const errorMessage = formErrors?.form ?? error.title ?? 'An unexpected error occurred';
                        toast.error(errorMessage);
                        return formErrors;
                    }

                    await Exceptionless.createException(error instanceof Error ? error : new Error(String(error)))
                        .setProperty('organizationId', organization.id)
                        .setProperty('selectedPlanId', value.selectedPlanId)
                        .addTags('billing', 'change-plan')
                        .submit();

                    const errorMessage = error instanceof Error ? error.message : 'An unexpected error occurred';
                    toast.error(errorMessage);
                    return { form: errorMessage };
                }
            }
        }
    }));

    // Sync reactive UI state into TanStack Form's imperative API.
    // untrack() prevents re-triggering the effect when setFieldValue updates form internals.
    $effect(() => {
        const planId = selectedPlanId;
        const mode = needsPayment ? 'new' : 'existing';
        const coupon = couponApplied ?? '';
        untrack(() => {
            form.setFieldValue('selectedPlanId', planId);
            form.setFieldValue('cardMode', mode);
            form.setFieldValue('couponId', coupon);
        });
    });

    // Initialize tier selection once plans load from the query.
    // untrack() prevents re-triggering when assignment to selectedTierId/interval creates a dependency cycle.
    $effect(() => {
        if (plansQuery.data) {
            untrack(() => {
                const nextTierIndex = currentTierIndex + 1;
                const upsellTier = nextTierIndex < tiers.length ? tiers[nextTierIndex] : null;

                if (isFreeCurrent && tiers.length > 0) {
                    selectedTierId = tiers[0]!.id;
                } else if (upsellTier) {
                    selectedTierId = upsellTier.id;
                } else {
                    selectedTierId = currentTierId === FREE_PLAN_ID ? '' : currentTierId;
                }
                // Always default to yearly to promote savings (especially for free→paid upgrades)
                interval = isFreeCurrent ? 'year' : currentInterval;
                paymentExpanded = false;
                couponOpen = initialCouponOpen ?? false;
                couponApplied = initialCouponCode ?? null;
                couponInput = '';
                couponError = null;
                form.reset();
            });
        }
    });

    function selectTier(tierId: string) {
        selectedTierId = tierId;
    }

    function setInterval(next: 'month' | 'year') {
        interval = next;
    }

    function onUseDifferentCard() {
        paymentExpanded = true;
        requestAnimationFrame(() => paymentSectionEl?.scrollIntoView({ behavior: 'smooth', block: 'nearest' }));
    }

    function onKeepCurrentCard() {
        paymentExpanded = false;
    }

    function onCouponOpen() {
        couponOpen = true;
        couponError = null;
        requestAnimationFrame(() => {
            couponSectionEl?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            couponInputEl?.focus();
        });
    }

    function onCouponCancel() {
        couponOpen = false;
        couponInput = '';
        couponError = null;
    }

    function onCouponApply() {
        const code = couponInput.trim();
        if (!code) {
            return;
        }

        couponApplied = code;
        couponOpen = false;
        couponInput = '';
        couponError = null;
    }

    function onCouponRemove() {
        couponApplied = null;
        couponError = null;
    }

    function handleCancel() {
        onclose(false);
    }

    function formatUsers(n: number): string {
        if (n < 0) {
            return 'Unlimited users';
        }

        return `${n} user${n === 1 ? '' : 's'}`;
    }

    function formatRetention(days: number): string {
        return `${days} day${days === 1 ? '' : 's'}`;
    }

    function tierPrice(tier: PlanTier, billingInterval: 'month' | 'year') {
        if (billingInterval === 'year' && tier.yearly) {
            return {
                amount: tier.yearly.price,
                period: '/yr',
                subAmount: tier.yearly.price / 12,
                subPeriod: '/mo'
            };
        }

        const plan = tier.monthly ?? tier.yearly;
        if (!plan) {
            return { amount: 0, period: '', subAmount: null, subPeriod: '' };
        }

        return { amount: plan.price, period: '/mo', subAmount: null, subPeriod: '' };
    }

    const yearlySavingsLabel = $derived.by(() => {
        const percentages = tiers
            .filter((t) => t.monthly && t.yearly && t.monthly.price > 0)
            .map((t) => {
                const fullYear = t.monthly!.price * 12;
                const saved = fullYear - t.yearly!.price;

                return Math.round((saved / fullYear) * 100);
            });

        if (percentages.length === 0) {
            return null;
        }

        const allSame = percentages.every((pct) => pct === percentages[0]);
        const display = allSame ? percentages[0] : Math.min(...percentages);

        if (!display || display <= 0) {
            return null;
        }

        return allSame ? `Save ${display}%` : `Save ~${display}%`;
    });

    function intervalWord(billingInterval: 'month' | 'year'): string {
        return billingInterval === 'year' ? 'yearly' : 'monthly';
    }

    function planLabel(planId: string, opts: { includeInterval?: boolean } = {}): string {
        if (planId === FREE_PLAN_ID) {
            return 'Free';
        }

        const tier = tiers.find((t) => t.id === tierOf(planId));
        if (!tier) {
            return planId;
        }

        return opts.includeInterval ? `${tier.name} ${intervalWord(intervalOf(planId))}` : tier.name;
    }

    const currentPlanInfo = $derived.by(() => {
        if (organization.plan_id === FREE_PLAN_ID) {
            return { isFree: true as const, label: 'Free plan', period: '', price: 0 };
        }

        const plan = plansQuery.data?.find((p: BillingPlan) => p.id === organization.plan_id);
        const price = organization.billing_price > 0 ? organization.billing_price : (plan?.price ?? 0);
        const name = tiers.find((t) => t.id === currentTierId)?.name ?? organization.plan_name;
        const period = currentInterval === 'year' ? '/yr' : '/mo';
        return { billedLabel: `billed ${intervalWord(currentInterval)}`, isFree: false as const, name, period, price };
    });

    const ctaLabel = $derived.by(() => {
        if (!anyDirty) {
            return 'Save changes';
        }

        if (planDirty && isFreeSelected) {
            return 'Downgrade to Free';
        }

        if (planDirty && organization.plan_id === FREE_PLAN_ID) {
            return `Start ${planLabel(selectedPlanId, { includeInterval: true })}`;
        }

        if (planDirty) {
            const intervalChanged = intervalOf(organization.plan_id) !== intervalOf(selectedPlanId);

            return isDowngrade
                ? `Downgrade to ${planLabel(selectedPlanId, { includeInterval: intervalChanged })}`
                : `Switch to ${planLabel(selectedPlanId, { includeInterval: intervalChanged })}`;
        }

        if (paymentDirty && !couponDirty) {
            return 'Update payment method';
        }

        if (couponDirty && !paymentDirty) {
            return 'Apply coupon';
        }

        return 'Save changes';
    });
</script>

<Dialog.Root
    open={true}
    onOpenChange={(v) => {
        if (!v) onclose(false);
    }}
>
    <Dialog.Content class="max-w-xl sm:max-w-xl">
        <Dialog.Header class="space-y-1">
            <Dialog.Title class="flex items-center gap-2 text-base">
                <CreditCard class="size-4" />
                Manage subscription
            </Dialog.Title>
            <Muted class="text-xs">
                <Small class="text-foreground text-xs">{organization.name}</Small>
                <span> · </span>
                {#if currentPlanInfo.isFree}
                    {currentPlanInfo.label}
                {:else}
                    {currentPlanInfo.name} · <Currency value={currentPlanInfo.price} />{currentPlanInfo.period}, {currentPlanInfo.billedLabel}
                {/if}
            </Muted>
        </Dialog.Header>

        {#if !isStripeEnabled()}
            <div class="py-4">
                <ErrorMessage message="Billing is currently disabled." />
            </div>
        {:else if plansQuery.isLoading}
            <div class="space-y-3 py-4">
                <Skeleton class="h-9 w-full" />
                <Skeleton class="h-40 w-full" />
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
                <div class="max-h-[70vh] space-y-6 overflow-y-auto px-1 py-1">
                    <section class="space-y-2.5">
                        <div class="flex items-center justify-between px-0.5">
                            <div class="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">Plan</div>
                            <Muted class="text-xs font-normal normal-case">All changes prorated</Muted>
                        </div>

                        <Tabs.Root value={interval} onValueChange={(value) => setInterval(value as 'month' | 'year')} class="w-full">
                            <Tabs.List class="grid w-full grid-cols-2">
                                <Tabs.Trigger value="month">Monthly</Tabs.Trigger>
                                <Tabs.Trigger value="year" class="gap-2">
                                    Yearly
                                    {#if yearlySavingsLabel}
                                        <Badge variant="default" class="text-foreground px-1.5 py-0 text-[10px]">{yearlySavingsLabel}</Badge>
                                    {/if}
                                </Tabs.Trigger>
                            </Tabs.List>
                        </Tabs.Root>

                        <div class="divide-border bg-card divide-y overflow-hidden rounded-lg border">
                            {#each tiers as tier, tierIdx (tier.id)}
                                {@const planForInterval = interval === 'year' && tier.yearly ? tier.yearly : tier.monthly}
                                {@const price = tierPrice(tier, interval)}
                                {@const isCurrent = tier.id === currentTierId && (interval === currentInterval || !tier.yearly)}
                                {@const isSelected = tier.id === selectedTierId}
                                <Button
                                    type="button"
                                    variant="ghost"
                                    onclick={() => selectTier(tier.id)}
                                    class="group relative flex h-auto w-full items-center gap-3 rounded-none px-4 py-3 text-left {isSelected
                                        ? 'bg-primary/5 hover:bg-primary/5'
                                        : 'hover:bg-muted/50'}"
                                >
                                    {#if isSelected}
                                        <span class="bg-primary absolute top-0 bottom-0 left-0 w-0.5"></span>
                                    {/if}
                                    <div class="min-w-0 flex-1">
                                        <div class="flex items-center gap-2">
                                            <span class="text-sm font-semibold tracking-tight">{tier.name}</span>
                                            {#if isCurrent}
                                                <Badge variant="secondary" class="px-1.5 py-0 text-[10px] uppercase">Current</Badge>
                                            {:else if tier.popular && tierIdx > currentTierIndex}
                                                <Badge variant="default" class="text-foreground px-1.5 py-0 text-[10px] uppercase">Most popular</Badge>
                                            {/if}
                                        </div>
                                        {#if planForInterval}
                                            <Muted class="mt-0.5 text-xs">
                                                {#if planForInterval.max_events_per_month < 0}Unlimited events{:else}<NumberCompact
                                                        value={planForInterval.max_events_per_month}
                                                    /> events/mo{/if}
                                                <span class="text-muted-foreground/60 mx-1">·</span>
                                                {formatRetention(planForInterval.retention_days)}
                                                <span class="text-muted-foreground/60 mx-1">·</span>
                                                {formatUsers(planForInterval.max_users)}
                                            </Muted>
                                        {/if}
                                    </div>
                                    <div class="text-right whitespace-nowrap">
                                        <div>
                                            <span class="text-sm font-semibold"><Currency value={price.amount} /></span><Muted
                                                class="inline text-xs font-medium">{price.period}</Muted
                                            >
                                        </div>
                                        {#if price.subAmount !== null}
                                            <Muted class="text-[11px]">~<Currency value={price.subAmount} />{price.subPeriod}</Muted>
                                        {/if}
                                    </div>
                                </Button>
                            {/each}

                            <Button
                                type="button"
                                variant="ghost"
                                onclick={() => selectTier('')}
                                class="group relative flex h-auto w-full items-center gap-3 rounded-none px-4 py-3 text-left {isFreeSelected
                                    ? 'bg-primary/5 hover:bg-primary/5'
                                    : 'hover:bg-muted/50'}"
                            >
                                {#if isFreeSelected}
                                    <span class="bg-primary absolute top-0 bottom-0 left-0 w-0.5"></span>
                                {/if}
                                <div class="min-w-0 flex-1">
                                    <div class="flex items-center gap-2">
                                        <span class="text-muted-foreground text-sm font-medium">Free</span>
                                        {#if isFreeCurrent}
                                            <Badge variant="secondary" class="px-1.5 py-0 text-[10px] uppercase">Current</Badge>
                                        {:else}
                                            <Muted class="text-xs">— cancel paid plan</Muted>
                                        {/if}
                                    </div>
                                    {#if freePlan}
                                        <Muted class="mt-0.5 text-xs">
                                            {#if freePlan.max_events_per_month < 0}Unlimited events{:else}<NumberCompact
                                                    value={freePlan.max_events_per_month}
                                                /> events/mo{/if}
                                            <span class="text-muted-foreground/60 mx-1">·</span>
                                            {formatRetention(freePlan.retention_days)}
                                            <span class="text-muted-foreground/60 mx-1">·</span>
                                            {formatUsers(freePlan.max_users)}
                                        </Muted>
                                    {/if}
                                </div>
                                <div class="text-right whitespace-nowrap">
                                    <Muted class="text-sm font-medium">Free</Muted>
                                </div>
                            </Button>
                        </div>
                    </section>

                    {#if isPaidPlan}
                        <section class="space-y-2.5" bind:this={paymentSectionEl}>
                            <div class="flex items-center justify-between px-0.5">
                                <div class="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">Payment method</div>
                                {#if hasExistingCard && paymentExpanded}
                                    <Button
                                        type="button"
                                        variant="link"
                                        class="text-muted-foreground h-auto p-0 text-xs font-medium normal-case"
                                        onclick={onKeepCurrentCard}
                                    >
                                        Keep current card
                                    </Button>
                                {/if}
                            </div>

                            {#if hasExistingCard && !paymentExpanded}
                                <div class="flex items-center justify-between gap-3 px-0.5 py-1">
                                    <div class="text-foreground flex items-center gap-2.5 text-sm">
                                        <CreditCard class="text-muted-foreground size-4" />
                                        <span>
                                            Paying with
                                            <Muted class="inline font-mono text-xs">···· {organization.card_last4}</Muted>
                                        </span>
                                    </div>
                                    <Button type="button" variant="link" class="text-primary h-auto p-0 text-sm font-medium" onclick={onUseDifferentCard}>
                                        Use a different payment method
                                    </Button>
                                </div>
                            {:else}
                                <div class="border-border bg-muted/30 rounded-lg border p-3">
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
                        </section>
                    {/if}

                    {#if !isFreeSelected}
                        <section class="space-y-2.5" bind:this={couponSectionEl}>
                            <div class="flex items-center justify-between px-0.5">
                                <div class="text-muted-foreground text-[11px] font-semibold tracking-wider uppercase">Coupon</div>
                                {#if couponOpen && !couponApplied}
                                    <Button
                                        type="button"
                                        variant="link"
                                        class="text-muted-foreground h-auto p-0 text-xs font-medium normal-case"
                                        onclick={onCouponCancel}
                                    >
                                        Cancel
                                    </Button>
                                {/if}
                            </div>

                            {#if couponApplied}
                                <Alert.Root variant="success" class="flex items-center gap-2.5 py-2.5">
                                    <Check class="size-4" />
                                    <Alert.Description class="flex flex-1 items-center gap-2">
                                        <span class="font-mono text-xs font-semibold">{couponApplied}</span>
                                        <span class="text-muted-foreground truncate">— will be applied</span>
                                        <span class="flex-1"></span>
                                        <Button type="button" variant="link" class="text-muted-foreground h-auto p-0 text-xs" onclick={onCouponRemove}>
                                            Remove
                                        </Button>
                                    </Alert.Description>
                                </Alert.Root>
                            {:else if couponOpen}
                                <div class="flex items-center gap-2">
                                    <Input
                                        type="text"
                                        placeholder="Enter code"
                                        autocomplete="off"
                                        bind:value={couponInput}
                                        bind:ref={couponInputEl}
                                        class={couponError ? 'border-destructive' : ''}
                                        oninput={() => {
                                            couponError = null;
                                        }}
                                        onkeydown={(e) => {
                                            if (e.key === 'Enter') {
                                                e.preventDefault();
                                                onCouponApply();
                                            }
                                        }}
                                    />
                                    <Button type="button" variant="outline" onclick={onCouponApply} disabled={!couponInput.trim()}>Apply</Button>
                                </div>
                                {#if couponError}
                                    <ErrorMessage message={couponError} />
                                {/if}
                            {:else}
                                <Button
                                    type="button"
                                    variant="outline"
                                    class="border-border/80 text-muted-foreground hover:text-foreground w-full justify-start gap-2 border-dashed"
                                    onclick={onCouponOpen}
                                >
                                    <Plus class="size-3.5" />
                                    Have a coupon code?
                                </Button>
                            {/if}
                        </section>
                    {/if}
                </div>

                <Dialog.Footer class="border-border mt-4 flex-col items-stretch gap-3 border-t pt-4 sm:flex-col sm:items-stretch sm:space-x-0">
                    <form.Subscribe selector={(state) => state.errors}>
                        {#snippet children(errors)}
                            <ErrorMessage message={getFormErrorMessages(errors)} />
                        {/snippet}
                    </form.Subscribe>

                    {#if initialFormError}
                        <ErrorMessage message={initialFormError} />
                    {/if}

                    <div class="min-h-4.5 space-y-1">
                        {#if !anyDirty}
                            <Muted class="text-xs italic">No changes yet</Muted>
                        {/if}
                        {#if planDirty}
                            {@const intervalChanged = intervalOf(organization.plan_id) !== intervalOf(selectedPlanId)}
                            {@const includeInt = intervalChanged || organization.plan_id === FREE_PLAN_ID || isFreeSelected}
                            <div class="flex items-baseline gap-2 text-xs leading-snug">
                                <Muted class="min-w-16 text-[10px] font-semibold tracking-wider uppercase">Plan</Muted>
                                <Muted class="text-xs">
                                    {#if isFreeSelected}
                                        <Small class="text-foreground text-xs">{planLabel(organization.plan_id, { includeInterval: true })}</Small>
                                        <span class="text-muted-foreground/60 mx-1">→</span>
                                        <Small class="text-foreground text-xs">Free</Small>
                                        · immediate, prorated credit
                                    {:else if organization.plan_id === FREE_PLAN_ID}
                                        Start <Small class="text-foreground text-xs">{planLabel(selectedPlanId, { includeInterval: true })}</Small>
                                        {#if selectedPlan}· <Currency value={selectedPlan.price} />{interval === 'year' ? '/yr' : '/mo'}{/if}
                                    {:else}
                                        <Small class="text-foreground text-xs">{planLabel(organization.plan_id, { includeInterval: includeInt })}</Small>
                                        <span class="text-muted-foreground/60 mx-1">→</span>
                                        <Small class="text-foreground text-xs">{planLabel(selectedPlanId, { includeInterval: includeInt })}</Small>
                                        {#if selectedPlan}· <Currency value={selectedPlan.price} />{interval === 'year' ? '/yr' : '/mo'} · prorated today{/if}
                                    {/if}
                                </Muted>
                            </div>
                        {/if}
                        {#if paymentDirty}
                            <div class="flex items-baseline gap-2 text-xs leading-snug">
                                <Muted class="min-w-16 text-[10px] font-semibold tracking-wider uppercase">Payment</Muted>
                                <Muted class="text-xs">
                                    ···· {organization.card_last4}
                                    <span class="text-muted-foreground/60 mx-1">→</span>
                                    <Small class="text-foreground text-xs">new payment method</Small>
                                </Muted>
                            </div>
                        {/if}
                        {#if couponDirty}
                            <div class="flex items-baseline gap-2 text-xs leading-snug">
                                <Muted class="min-w-16 text-[10px] font-semibold tracking-wider uppercase">Coupon</Muted>
                                <Muted class="text-xs">
                                    <Small class="text-foreground font-mono text-xs">{couponApplied}</Small> applied
                                </Muted>
                            </div>
                        {/if}
                    </div>

                    <form.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <div class="flex items-center justify-end gap-2">
                                <Button type="button" variant="outline" onclick={handleCancel} disabled={isSubmitting}>Cancel</Button>
                                <Button type="submit" variant={isDowngrade ? 'destructive' : 'default'} disabled={isSubmitting || !anyDirty}>
                                    {#if isSubmitting}
                                        <Spinner class="mr-2" />
                                        Saving…
                                    {:else}
                                        {ctaLabel}
                                    {/if}
                                </Button>
                            </div>
                        {/snippet}
                    </form.Subscribe>
                </Dialog.Footer>
            </form>
        {/if}
    </Dialog.Content>
</Dialog.Root>
