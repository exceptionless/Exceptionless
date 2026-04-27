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

    let stripeInstance: null | Stripe = null;
    let elementsInstance: null | StripeElements = null;
    let paymentElement: null | StripePaymentElement = null;
    let errorMessage: null | string = $state(null);
    let disposed = false;

    let skeletonDiv: HTMLDivElement;
    let paymentDiv: HTMLDivElement;
    let errorDiv: HTMLDivElement;

    onMount(() => {
        async function init() {
            try {
                const stripe = await loadStripeOnce();
                if (disposed) return;

                if (!stripe) {
                    showError('Stripe is not configured. Please contact support.');
                    return;
                }

                stripeInstance = stripe;
                elementsInstance = clientSecret
                    ? stripe.elements({ appearance, clientSecret })
                    : stripe.elements({
                          appearance,
                          currency: currency ?? 'usd',
                          mode: mode ?? 'setup',
                          paymentMethodCreation: 'manual'
                      });

                paymentElement = elementsInstance.create('payment');
                paymentElement.mount(paymentDiv);

                skeletonDiv.style.display = 'none';
                paymentDiv.style.display = 'block';

                onload?.(stripe);
                onElementsChange?.(elementsInstance);
            } catch (ex) {
                if (disposed) return;
                showError(ex instanceof Error ? ex.message : 'Failed to load payment system');
            }
        }

        init();

        return () => {
            disposed = true;
            paymentElement?.destroy();
        };
    });

    function showError(msg: string) {
        errorMessage = msg;
        skeletonDiv.style.display = 'none';
        paymentDiv.style.display = 'none';
        errorDiv.style.display = 'block';
    }

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
