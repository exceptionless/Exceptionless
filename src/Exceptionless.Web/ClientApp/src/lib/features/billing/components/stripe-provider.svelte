<script lang="ts">
    import type { Stripe, StripeElements, StripeElementsOptions, StripePaymentElement } from '@stripe/stripe-js';

    import ErrorMessage from '$comp/error-message.svelte';
    import { loadStripeOnce, setStripeContext } from '$features/billing/stripe.svelte';
    import { onMount, setContext } from 'svelte';

    type Props = (
        | { clientSecret: string; mode?: never }
        | { clientSecret?: never; mode?: 'setup' }
    ) & {
        appearance?: StripeElementsOptions['appearance'];
        currency?: string;
        onElementsChange?: (elements: StripeElements | undefined) => void;
        onload?: (stripe: Stripe) => void;
    };

    let { appearance, clientSecret, currency = 'usd', mode = 'setup', onElementsChange, onload }: Props = $props();

    /**
     * Fully imperative approach: bypasses both svelte-stripe's <Elements> and
     * <PaymentElement> components, and avoids Svelte 5 reactive template
     * guards entirely.
     *
     * Why: Svelte 5 has a reactivity issue where $state/$derived/stores set
     * from async callbacks (onMount, .then) don't reliably trigger template
     * {#if} re-renders. We sidestep this by using direct DOM manipulation
     * for show/hide and mounting the Stripe PaymentElement imperatively.
     */

    let stripeInstance: Stripe | null = null;
    let elementsInstance: StripeElements | null = null;
    let paymentElement: StripePaymentElement | null = null;
    let errorMessage: string | null = null;

    let skeletonDiv: HTMLDivElement;
    let paymentDiv: HTMLDivElement;
    let errorDiv: HTMLDivElement;

    onMount(() => {
        loadStripeOnce()
            .then((stripe) => {
                if (!stripe) {
                    showError('Stripe is not configured. Please contact support.');
                    return;
                }

                stripeInstance = stripe;
                elementsInstance = clientSecret
                    ? stripe.elements({ clientSecret, appearance })
                    : stripe.elements({ mode: mode ?? 'setup', currency: currency ?? 'usd', appearance });

                paymentElement = elementsInstance.create('payment');
                paymentElement.mount(paymentDiv);

                // Swap skeleton for payment element
                skeletonDiv.style.display = 'none';
                paymentDiv.style.display = 'block';

                onload?.(stripe);
                onElementsChange?.(elementsInstance);
            })
            .catch((ex) => {
                showError(ex instanceof Error ? ex.message : 'Failed to load payment system');
            });

        return () => {
            paymentElement?.destroy();
        };
    });

    function showError(msg: string) {
        errorMessage = msg;
        skeletonDiv.style.display = 'none';
        paymentDiv.style.display = 'none';
        errorDiv.style.display = 'block';
    }

    // Provide context for svelte-stripe's PaymentElement (in case it's used)
    // and for useStripe() in form submission handlers.
    setContext('stripe', {
        get stripe() { return stripeInstance; },
        get elements() { return elementsInstance; }
    });

    setStripeContext({
        get elements() { return elementsInstance; },
        get error() { return errorMessage; },
        get isLoading() { return !elementsInstance && !errorMessage; },
        get stripe() { return stripeInstance; }
    });
</script>

<div bind:this={skeletonDiv} class="h-32 w-full animate-pulse rounded-md bg-accent"></div>
<div bind:this={paymentDiv} class="min-h-[200px]" style="display:none;"></div>
<div bind:this={errorDiv} style="display:none;">
    <ErrorMessage message={errorMessage ?? 'An error occurred'} />
</div>
