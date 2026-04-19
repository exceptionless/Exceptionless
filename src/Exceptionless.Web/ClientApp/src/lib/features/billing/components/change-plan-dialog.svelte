<script lang="ts">
    import type { ViewOrganization } from '$features/organizations/models';
    import type { BillingPlan } from '$lib/generated/api';
    import type { Stripe, StripeElements } from '@stripe/stripe-js';

    import ErrorMessage from '$comp/error-message.svelte';
    import { Button } from '$comp/ui/button';
    import * as Dialog from '$comp/ui/dialog';
    import { Input } from '$comp/ui/input';
    import { Skeleton } from '$comp/ui/skeleton';
    import { Spinner } from '$comp/ui/spinner';
    import { FREE_PLAN_ID, isStripeEnabled, StripeProvider } from '$features/billing';
    import { type ChangePlanFormData, ChangePlanSchema } from '$features/billing/schemas';
    import { changePlanMutation, getPlansQuery } from '$features/organizations/api.svelte';
    import { getFormErrorMessages } from '$features/shared/validation';
    import Check from '@lucide/svelte/icons/check';
    import CreditCard from '@lucide/svelte/icons/credit-card';
    import Plus from '@lucide/svelte/icons/plus';
    import X from '@lucide/svelte/icons/x';
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

    // ───────────────────────────────────────────────────────────────
    // Plan tier / interval model
    //
    // Backend ships 11 plan IDs: EX_FREE + 5 monthly (EX_SMALL, EX_MEDIUM,
    // EX_LARGE, EX_XL, EX_ENT) + 5 yearly (…_YEARLY). The monthly/yearly
    // toggle is derived UI state — we group plans by tier key (strip
    // _YEARLY) and submit the concrete plan ID on save.
    // ───────────────────────────────────────────────────────────────
    const YEARLY_SUFFIX = '_YEARLY';
    // Hand-curated "most popular" tier — backend doesn't carry this flag.
    const POPULAR_TIER = 'EX_MEDIUM';

    function tierOf(planId: string): string {
        return planId.endsWith(YEARLY_SUFFIX) ? planId.slice(0, -YEARLY_SUFFIX.length) : planId;
    }
    function intervalOf(planId: string): 'month' | 'year' {
        return planId.endsWith(YEARLY_SUFFIX) ? 'year' : 'month';
    }

    interface PlanTier {
        id: string; // tier key — matches the monthly plan ID
        name: string; // e.g. "Small"
        monthly: BillingPlan | null;
        yearly: BillingPlan | null;
        popular: boolean;
    }

    const tiers = $derived.by<PlanTier[]>(() => {
        if (!plansQuery.data) return [];
        const byTier = new Map<string, PlanTier>();
        for (const p of plansQuery.data) {
            if (p.is_hidden || p.id === FREE_PLAN_ID) continue;
            const key = tierOf(p.id);
            const current = byTier.get(key) ?? {
                id: key,
                name: p.name.replace(/\s*\(Yearly\)\s*$/i, '').trim(),
                monthly: null,
                yearly: null,
                popular: key === POPULAR_TIER
            };
            if (intervalOf(p.id) === 'year') current.yearly = p;
            else current.monthly = p;
            byTier.set(key, current);
        }
        // Preserve backend ordering by first-seen monthly tier
        const ordered: PlanTier[] = [];
        for (const p of plansQuery.data) {
            if (p.is_hidden || p.id === FREE_PLAN_ID) continue;
            const key = tierOf(p.id);
            const tier = byTier.get(key);
            if (tier && !ordered.includes(tier)) ordered.push(tier);
        }
        return ordered;
    });
    const freePlan = $derived(plansQuery.data?.find((p: BillingPlan) => p.id === FREE_PLAN_ID) ?? null);
    const isFreeCurrent = $derived(organization.plan_id === FREE_PLAN_ID);

    const currentInterval = $derived<'month' | 'year'>(intervalOf(organization.plan_id));
    const currentTierId = $derived(tierOf(organization.plan_id));

    // ───────────────────────────────────────────────────────────────
    // UI state
    // ───────────────────────────────────────────────────────────────
    let selectedTierId = $state<string>(''); // '' = Free
    let interval = $state<'month' | 'year'>('month');
    let paymentExpanded = $state(false);
    let couponOpen = $state(false);
    let couponApplied = $state<null | string>(null);
    let couponInput = $state('');

    const hasExistingCard = $derived(!!organization.card_last4);

    // Resolve selected plan ID from tier + interval, falling back to monthly
    // when the tier doesn't offer a yearly option.
    function resolvePlanId(tierId: string, iv: 'month' | 'year'): string {
        if (!tierId) return FREE_PLAN_ID;
        const tier = tiers.find((t) => t.id === tierId);
        if (!tier) return FREE_PLAN_ID;
        if (iv === 'year' && tier.yearly) return tier.yearly.id;
        return tier.monthly?.id ?? tier.yearly?.id ?? FREE_PLAN_ID;
    }

    const selectedPlanId = $derived(resolvePlanId(selectedTierId, interval));
    const selectedPlan = $derived(
        plansQuery.data?.find((p: BillingPlan) => p.id === selectedPlanId) ?? null
    );
    const isFreeSelected = $derived(selectedPlanId === FREE_PLAN_ID);
    const isPaidPlan = $derived(!!selectedPlan && selectedPlan.price > 0);
    const isCurrentPlan = $derived(selectedPlanId === organization.plan_id);

    // Dirty tracking
    const planDirty = $derived(!isCurrentPlan);
    const paymentDirty = $derived(paymentExpanded && hasExistingCard);
    const needsPayment = $derived(isPaidPlan && (!hasExistingCard || paymentExpanded));
    const couponDirty = $derived(!!couponApplied);
    const anyDirty = $derived(planDirty || paymentDirty || couponDirty);

    // ───────────────────────────────────────────────────────────────
    // Stripe wiring
    // ───────────────────────────────────────────────────────────────
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

    // ───────────────────────────────────────────────────────────────
    // Keep form in sync with UI state (it's the canonical submit value)
    // ───────────────────────────────────────────────────────────────
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

    // Reset dialog state when it opens
    $effect(() => {
        if (open && plansQuery.data) {
            untrack(() => {
                // Default to current plan so "Save changes" is disabled at open
                selectedTierId = currentTierId === FREE_PLAN_ID ? '' : currentTierId;
                interval = currentInterval;
                paymentExpanded = false;
                couponOpen = false;
                couponApplied = null;
                couponInput = '';
                form.reset();
            });
        }
    });

    // ───────────────────────────────────────────────────────────────
    // Handlers
    // ───────────────────────────────────────────────────────────────
    function selectTier(tierId: string) {
        selectedTierId = tierId;
    }

    function setInterval(next: 'month' | 'year') {
        interval = next;
    }

    function onUseDifferentCard() {
        paymentExpanded = true;
    }

    function onKeepCurrentCard() {
        paymentExpanded = false;
    }

    function onCouponOpen() {
        couponOpen = true;
    }

    function onCouponCancel() {
        couponOpen = false;
        couponInput = '';
    }

    function onCouponApply() {
        const code = couponInput.trim();
        if (!code) return;
        couponApplied = code.toUpperCase();
        couponOpen = false;
        couponInput = '';
    }

    function onCouponRemove() {
        couponApplied = null;
    }

    function handleCancel() {
        open = false;
    }

    // ───────────────────────────────────────────────────────────────
    // Display helpers
    // ───────────────────────────────────────────────────────────────
    function formatEvents(n: number): string {
        if (n < 0) return 'Unlimited events';
        if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(n % 1_000_000 === 0 ? 0 : 1)}M events/mo`;
        if (n >= 1_000) return `${Math.round(n / 1_000)}K events/mo`;
        return `${n} events/mo`;
    }
    function formatUsers(n: number): string {
        return n < 0 ? 'Unlimited users' : `${n} users`;
    }
    function formatRetention(days: number): string {
        return `${days} day${days === 1 ? '' : 's'}`;
    }
    function tierPrice(tier: PlanTier, iv: 'month' | 'year') {
        if (iv === 'year' && tier.yearly) {
            const perMonth = tier.yearly.price / 12;
            return {
                amount: `$${tier.yearly.price}`,
                period: '/yr',
                sub: `~$${perMonth.toFixed(0)}/mo`
            };
        }
        const p = tier.monthly ?? tier.yearly;
        if (!p) return { amount: '—', period: '', sub: '' };
        return { amount: `$${p.price}`, period: '/mo', sub: '' };
    }
    const yearlySavingsLabel = $derived.by(() => {
        const sample = tiers.find((t) => t.monthly && t.yearly);
        if (!sample?.monthly || !sample.yearly) return null;
        const fullYear = sample.monthly.price * 12;
        const saved = fullYear - sample.yearly.price;
        const pct = Math.round((saved / fullYear) * 100);
        return pct > 0 ? `Save ${pct}%` : null;
    });
    function intervalWord(iv: 'month' | 'year'): string {
        return iv === 'year' ? 'yearly' : 'monthly';
    }
    function planLabel(planId: string): string {
        if (planId === FREE_PLAN_ID) return 'Free';
        const tier = tiers.find((t) => t.id === tierOf(planId));
        if (!tier) return planId;
        return `${tier.name} ${intervalWord(intervalOf(planId))}`;
    }

    const currentSubtitle = $derived.by(() => {
        if (organization.plan_id === FREE_PLAN_ID) return 'Free plan';
        const tier = tiers.find((t) => t.id === currentTierId);
        const plan = plansQuery.data?.find((p: BillingPlan) => p.id === organization.plan_id);
        if (!tier || !plan) return organization.plan_name;
        const period = currentInterval === 'year' ? '/yr' : '/mo';
        return `${tier.name} · $${plan.price}${period}, billed ${intervalWord(currentInterval)}`;
    });

    // CTA label — mirrors the mockup's dirty-aware logic
    const ctaLabel = $derived.by(() => {
        if (!anyDirty) return 'Save changes';
        if (planDirty && isFreeSelected) return 'Downgrade to Free';
        if (planDirty && organization.plan_id === FREE_PLAN_ID) return `Start ${planLabel(selectedPlanId)}`;
        if (planDirty) return `Switch to ${planLabel(selectedPlanId)}`;
        if (paymentDirty && !couponDirty) return 'Update payment method';
        if (couponDirty && !paymentDirty) return 'Apply coupon';
        return 'Save changes';
    });
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="max-w-xl sm:max-w-xl">
        <Dialog.Header class="space-y-1">
            <Dialog.Title class="flex items-center gap-2 text-base">
                <CreditCard class="size-4" />
                Manage subscription
            </Dialog.Title>
            <p class="text-muted-foreground text-xs">
                <strong class="text-foreground font-medium">{organization.name}</strong>
                <span class="text-muted-foreground"> · </span>
                {currentSubtitle}
            </p>
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
                <form.Subscribe selector={(state) => state.errors}>
                    {#snippet children(errors)}
                        <ErrorMessage message={getFormErrorMessages(errors)}></ErrorMessage>
                    {/snippet}
                </form.Subscribe>

                <div class="max-h-[70vh] space-y-6 overflow-y-auto py-1 pr-0.5">
                    <!-- ── Plan section ─────────────────────────────────────── -->
                    <section class="space-y-2.5">
                        <div class="flex items-center justify-between px-0.5">
                            <div class="text-muted-foreground flex items-center gap-2 text-[11px] font-semibold tracking-wider uppercase">
                                Plan
                                {#if planDirty}
                                    <span class="bg-primary ring-primary/20 inline-block size-1.5 rounded-full ring-2"></span>
                                {/if}
                            </div>
                            <span class="text-muted-foreground text-xs font-normal normal-case">All changes prorated</span>
                        </div>

                        <!-- Monthly / Yearly toggle -->
                        <div class="bg-muted grid grid-cols-2 gap-0.5 rounded-lg p-0.5">
                            <button
                                type="button"
                                class="inline-flex items-center justify-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors {interval === 'month' ? 'bg-background text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'}"
                                onclick={() => setInterval('month')}
                            >
                                Monthly
                            </button>
                            <button
                                type="button"
                                class="inline-flex items-center justify-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors {interval === 'year' ? 'bg-background text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'}"
                                onclick={() => setInterval('year')}
                            >
                                Yearly
                                {#if yearlySavingsLabel}
                                    <span class="bg-primary/10 text-primary border-primary/30 rounded-full border px-1.5 py-0 text-[10px] font-semibold tracking-wide">{yearlySavingsLabel}</span>
                                {/if}
                            </button>
                        </div>

                        <!-- Plan list -->
                        <div class="divide-border bg-card divide-y overflow-hidden rounded-lg border">
                            {#each tiers as tier (tier.id)}
                                {@const planForInterval = interval === 'year' && tier.yearly ? tier.yearly : tier.monthly}
                                {@const price = tierPrice(tier, interval)}
                                {@const isCurrent = tier.id === currentTierId && (interval === currentInterval || !tier.yearly)}
                                {@const isSelected = tier.id === selectedTierId}
                                <button
                                    type="button"
                                    onclick={() => selectTier(tier.id)}
                                    class="group relative flex w-full items-center gap-3 px-4 py-3 text-left transition-colors {isSelected ? 'bg-primary/5' : 'hover:bg-muted/50'}"
                                >
                                    {#if isSelected}
                                        <span class="bg-primary absolute top-0 bottom-0 left-0 w-0.5"></span>
                                    {/if}
                                    <div class="min-w-0 flex-1">
                                        <div class="flex items-center gap-2">
                                            <span class="text-sm font-semibold tracking-tight">{tier.name}</span>
                                            {#if isCurrent}
                                                <span class="text-muted-foreground border-border bg-muted/60 rounded-full border px-1.5 py-0 text-[10px] font-semibold tracking-wide uppercase">Current</span>
                                            {:else if tier.popular}
                                                <span class="text-primary border-primary/30 bg-primary/10 rounded-full border px-1.5 py-0 text-[10px] font-semibold tracking-wide uppercase">Most popular</span>
                                            {/if}
                                        </div>
                                        {#if planForInterval}
                                            <div class="text-muted-foreground mt-0.5 text-xs">
                                                {formatEvents(planForInterval.max_events_per_month)}
                                                <span class="text-muted-foreground/60 mx-1">·</span>
                                                {formatRetention(planForInterval.retention_days)}
                                                <span class="text-muted-foreground/60 mx-1">·</span>
                                                {formatUsers(planForInterval.max_users)}
                                            </div>
                                        {/if}
                                    </div>
                                    <div class="text-right whitespace-nowrap">
                                        <div>
                                            <span class="text-sm font-semibold">{price.amount}</span><span class="text-muted-foreground text-xs font-medium">{price.period}</span>
                                        </div>
                                        {#if price.sub}
                                            <div class="text-muted-foreground/80 text-[11px]">{price.sub}</div>
                                        {/if}
                                    </div>
                                </button>
                            {/each}

                            <!-- Free -->
                            <button
                                type="button"
                                onclick={() => selectTier('')}
                                class="group relative flex w-full items-center gap-3 px-4 py-3 text-left transition-colors {isFreeSelected ? 'bg-primary/5' : 'hover:bg-muted/50'}"
                            >
                                {#if isFreeSelected}
                                    <span class="bg-primary absolute top-0 bottom-0 left-0 w-0.5"></span>
                                {/if}
                                <div class="min-w-0 flex-1">
                                    <div class="flex items-center gap-2">
                                        <span class="text-muted-foreground text-sm font-medium">Free</span>
                                        {#if isFreeCurrent}
                                            <span class="text-muted-foreground border-border bg-muted/60 rounded-full border px-1.5 py-0 text-[10px] font-semibold tracking-wide uppercase">Current</span>
                                        {:else}
                                            <span class="text-muted-foreground/70 text-xs">— cancel paid plan</span>
                                        {/if}
                                    </div>
                                    {#if freePlan}
                                        <div class="text-muted-foreground mt-0.5 text-xs">
                                            {formatEvents(freePlan.max_events_per_month)}
                                            <span class="text-muted-foreground/60 mx-1">·</span>
                                            {formatRetention(freePlan.retention_days)}
                                            <span class="text-muted-foreground/60 mx-1">·</span>
                                            {formatUsers(freePlan.max_users)}
                                        </div>
                                    {/if}
                                </div>
                                <div class="text-right whitespace-nowrap">
                                    <span class="text-muted-foreground text-sm font-medium">Free</span>
                                </div>
                            </button>
                        </div>
                    </section>

                    <!-- ── Payment section ────────────────────────────────── -->
                    {#if isPaidPlan}
                        <section class="space-y-2.5">
                            <div class="flex items-center justify-between px-0.5">
                                <div class="text-muted-foreground flex items-center gap-2 text-[11px] font-semibold tracking-wider uppercase">
                                    Payment method
                                    {#if paymentDirty}
                                        <span class="bg-primary ring-primary/20 inline-block size-1.5 rounded-full ring-2"></span>
                                    {/if}
                                </div>
                                {#if hasExistingCard && paymentExpanded}
                                    <button
                                        type="button"
                                        onclick={onKeepCurrentCard}
                                        class="text-muted-foreground hover:text-foreground text-xs font-medium normal-case hover:underline"
                                    >
                                        Keep current card
                                    </button>
                                {/if}
                            </div>

                            {#if hasExistingCard && !paymentExpanded}
                                <div class="flex items-center justify-between gap-3 px-0.5 py-1">
                                    <div class="text-foreground flex items-center gap-2.5 text-sm">
                                        <CreditCard class="text-muted-foreground size-4" />
                                        <span>
                                            Paying with
                                            <span class="text-muted-foreground font-mono text-xs">···· {organization.card_last4}</span>
                                        </span>
                                    </div>
                                    <button
                                        type="button"
                                        onclick={onUseDifferentCard}
                                        class="text-primary text-sm font-medium hover:underline"
                                    >
                                        Use a different payment method
                                    </button>
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

                    <!-- ── Coupon section ─────────────────────────────────── -->
                    {#if !isFreeSelected}
                        <section class="space-y-2.5">
                            <div class="flex items-center justify-between px-0.5">
                                <div class="text-muted-foreground flex items-center gap-2 text-[11px] font-semibold tracking-wider uppercase">
                                    Coupon
                                    {#if couponDirty}
                                        <span class="bg-primary ring-primary/20 inline-block size-1.5 rounded-full ring-2"></span>
                                    {/if}
                                </div>
                                {#if couponOpen && !couponApplied}
                                    <button
                                        type="button"
                                        onclick={onCouponCancel}
                                        class="text-muted-foreground hover:text-foreground text-xs font-medium normal-case hover:underline"
                                    >
                                        Cancel
                                    </button>
                                {/if}
                            </div>

                            {#if couponApplied}
                                <div class="border-primary/30 bg-primary/5 flex items-center gap-2.5 rounded-lg border px-3 py-2.5 text-sm">
                                    <Check class="text-primary size-4 shrink-0" />
                                    <span class="text-primary font-mono text-xs font-semibold">{couponApplied}</span>
                                    <span class="text-muted-foreground truncate">— applied at checkout</span>
                                    <span class="flex-1"></span>
                                    <button
                                        type="button"
                                        onclick={onCouponRemove}
                                        class="text-muted-foreground hover:text-foreground text-xs hover:underline"
                                    >
                                        Remove
                                    </button>
                                </div>
                            {:else if couponOpen}
                                <div class="flex items-stretch gap-2">
                                    <Input
                                        type="text"
                                        placeholder="Enter code"
                                        autocomplete="off"
                                        class="font-mono"
                                        bind:value={couponInput}
                                        onkeydown={(e) => {
                                            if (e.key === 'Enter') {
                                                e.preventDefault();
                                                onCouponApply();
                                            }
                                        }}
                                    />
                                    <Button type="button" variant="outline" onclick={onCouponApply} disabled={!couponInput.trim()}>
                                        Apply
                                    </Button>
                                </div>
                            {:else}
                                <button
                                    type="button"
                                    onclick={onCouponOpen}
                                    class="border-border/80 text-muted-foreground hover:bg-muted/50 hover:text-foreground flex w-full items-center gap-2 rounded-lg border border-dashed px-3 py-2.5 text-left text-sm transition-colors"
                                >
                                    <Plus class="size-3.5" />
                                    Have a coupon code?
                                </button>
                            {/if}
                        </section>
                    {/if}
                </div>

                <Dialog.Footer class="border-border mt-4 flex-col items-stretch gap-3 border-t pt-4 sm:flex-col sm:items-stretch sm:space-x-0">
                    <!-- Change summary -->
                    <div class="min-h-[18px] space-y-1">
                        {#if !anyDirty}
                            <div class="text-muted-foreground text-xs italic">No changes yet</div>
                        {/if}
                        {#if planDirty}
                            <div class="flex items-baseline gap-2 text-xs leading-snug">
                                <span class="text-muted-foreground min-w-[64px] text-[10px] font-semibold tracking-wider uppercase">Plan</span>
                                <span class="text-muted-foreground">
                                    {#if isFreeSelected}
                                        <strong class="text-foreground font-medium">{planLabel(organization.plan_id)}</strong>
                                        <span class="text-muted-foreground/60 mx-1">→</span>
                                        <strong class="text-foreground font-medium">Free</strong>
                                        · immediate, prorated credit
                                    {:else if organization.plan_id === FREE_PLAN_ID}
                                        Start <strong class="text-foreground font-medium">{planLabel(selectedPlanId)}</strong>
                                        {#if selectedPlan}· ${selectedPlan.price}{interval === 'year' ? '/yr' : '/mo'}{/if}
                                    {:else}
                                        <strong class="text-foreground font-medium">{planLabel(organization.plan_id)}</strong>
                                        <span class="text-muted-foreground/60 mx-1">→</span>
                                        <strong class="text-foreground font-medium">{planLabel(selectedPlanId)}</strong>
                                        {#if selectedPlan}· ${selectedPlan.price}{interval === 'year' ? '/yr' : '/mo'} · prorated today{/if}
                                    {/if}
                                </span>
                            </div>
                        {/if}
                        {#if paymentDirty}
                            <div class="flex items-baseline gap-2 text-xs leading-snug">
                                <span class="text-muted-foreground min-w-[64px] text-[10px] font-semibold tracking-wider uppercase">Payment</span>
                                <span class="text-muted-foreground">
                                    ···· {organization.card_last4}
                                    <span class="text-muted-foreground/60 mx-1">→</span>
                                    <strong class="text-foreground font-medium">new payment method</strong>
                                </span>
                            </div>
                        {/if}
                        {#if couponDirty}
                            <div class="flex items-baseline gap-2 text-xs leading-snug">
                                <span class="text-muted-foreground min-w-[64px] text-[10px] font-semibold tracking-wider uppercase">Coupon</span>
                                <span class="text-muted-foreground">
                                    <strong class="text-foreground font-mono font-medium">{couponApplied}</strong> applied
                                </span>
                            </div>
                        {/if}
                    </div>

                    <form.Subscribe selector={(state) => state.isSubmitting}>
                        {#snippet children(isSubmitting)}
                            <div class="flex items-center justify-end gap-2">
                                <Button type="button" variant="outline" onclick={handleCancel} disabled={isSubmitting}>
                                    Cancel
                                </Button>
                                <Button type="submit" disabled={isSubmitting || !anyDirty}>
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
