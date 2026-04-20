<script lang="ts">
    import type { Stripe, StripeElements, StripeElementsOptions, StripePaymentElement } from '@stripe/stripe-js';

    import ErrorMessage from '$comp/error-message.svelte';
    import { loadStripeOnce, setStripeContext } from '$features/billing/stripe.svelte';
    import { onMount, setContext } from 'svelte';

    type Props = {
        appearance?: StripeElementsOptions['appearance'];
        currency?: string;
        onElementsChange?: (elements: StripeElements | undefined) => void;
        onload?: (stripe: Stripe) => void;
    } & ({ clientSecret: string; mode?: never } | { clientSecret?: never; mode?: 'setup' });

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

    let stripeInstance: null | Stripe = null;
    let elementsInstance: null | StripeElements = null;
    let paymentElement: null | StripePaymentElement = null;
    let errorMessage: null | string = $state(null);

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
                    ? stripe.elements({ appearance, clientSecret })
                    : stripe.elements({
                          // `paymentMethodCreation: 'manual'` is required when we plan
                          // to call `stripe.createPaymentMethod({ elements })` later
                          // (deferred-intent flow). Without it Stripe throws
                          // "your elements instance must be created with
                          // paymentMethodCreation: 'manual'".
                          appearance,
                          currency: currency ?? 'usd',
                          mode: mode ?? 'setup',
                          paymentMethodCreation: 'manual'
                      });

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
        get elements() {
            return elementsInstance;
        },
        get stripe() {
            return stripeInstance;
        }
    });

    setStripeContext({
        get elements() {
            return elementsInstance;
        },
        get error() {
            return errorMessage;
        },
        get isLoading() {
            return !elementsInstance && !errorMessage;
        },
        get stripe() {
            return stripeInstance;
        }
    });
</script>

<div bind:this={skeletonDiv} class="bg-accent h-32 w-full animate-pulse rounded-md"></div>
<div bind:this={paymentDiv} class="min-h-[200px]" style="display:none;"></div>
<div bind:this={errorDiv} style="display:none;">
    <ErrorMessage message={errorMessage ?? 'An error occurred'} />
</div>
