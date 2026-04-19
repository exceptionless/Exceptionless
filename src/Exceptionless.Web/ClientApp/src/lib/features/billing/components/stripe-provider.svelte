<script lang="ts">
    import type { Stripe, StripeElements, StripeElementsOptions } from '@stripe/stripe-js';
    import type { Snippet } from 'svelte';

    import ErrorMessage from '$comp/error-message.svelte';
    import { Skeleton } from '$comp/ui/skeleton';
    import { loadStripeOnce, setStripeContext } from '$features/billing/stripe.svelte';
    import { onMount } from 'svelte';
    import { Elements } from 'svelte-stripe';

    /**
     * Props use a discriminated union to enforce that `clientSecret` and `mode`
     * are mutually exclusive at compile time (Stripe Elements requires one or the other).
     */
    type Props = (
        | {
              /** Client secret for PaymentIntent/SetupIntent mode */
              clientSecret: string;
              mode?: never;
          }
        | {
              clientSecret?: never;
              /**
               * Elements mode — mutually exclusive with clientSecret in Stripe v9+.
               * Only 'setup' is supported without additional required fields (amount/currency).
               * To add 'payment' or 'subscription' mode, also add amount and currency props.
               */
              mode?: 'setup';
          }
    ) & {
        /** Optional appearance theme for Stripe Elements */
        appearance?: StripeElementsOptions['appearance'];
        /** Content to render inside Elements */
        children: Snippet;
        /** Callback when Stripe Elements state changes */
        onElementsChange?: (elements: StripeElements | undefined) => void;
        /** Callback when Stripe finishes loading */
        onload?: (stripe: Stripe) => void;
    };

    let { appearance, children, clientSecret, mode = 'setup', onElementsChange, onload }: Props = $props();

    let stripe = $state<null | Stripe>(null);
    let elements = $state<StripeElements | undefined>(undefined);
    let isLoading = $state(true);
    let error = $state<null | string>(null);

    // Set up context for child components using useStripe()
    setStripeContext({
        get elements() {
            return elements ?? null;
        },
        get error() {
            return error;
        },
        get isLoading() {
            return isLoading;
        },
        get stripe() {
            return stripe;
        }
    });

    onMount(async () => {
        try {
            stripe = await loadStripeOnce();
            if (!stripe) {
                error = 'Stripe is not configured. Please contact support.';
            } else {
                onload?.(stripe);
            }
        } catch (ex) {
            error = ex instanceof Error ? ex.message : 'Failed to load payment system';
        } finally {
            isLoading = false;
        }
    });

    $effect(() => {
        onElementsChange?.(elements);
    });
</script>

{#if isLoading}
    <Skeleton class="h-32 w-full" />
{:else if error}
    <ErrorMessage message={error} />
{:else if stripe}
    <!--
        Two branches are intentional: Stripe Elements requires either clientSecret OR mode,
        never both. Spreading a computed object would lose compile-time type checking against
        the Elements component's props. The duplication is minimal (3 lines each) and the
        intent is immediately clear.
    -->
    {#if clientSecret}
        <Elements {stripe} {clientSecret} {appearance} bind:elements>
            {@render children()}
        </Elements>
    {:else}
        <Elements {stripe} {mode} {appearance} bind:elements>
            {@render children()}
        </Elements>
    {/if}
{/if}
