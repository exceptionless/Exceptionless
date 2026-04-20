/**
 * Billing feature module - Stripe integration for plan management.
 */

// Components
export { default as ChangePlanDialog } from './components/change-plan-dialog.svelte';

export { default as StripeProvider } from './components/stripe-provider.svelte';

// Upgrade required handling
export { default as UpgradeRequiredDialog } from './components/upgrade-required-dialog.svelte';

// Constants
export { FREE_PLAN_ID } from './constants';
// Models
export type { BillingPlan, CardMode, ChangePlanFormState, ChangePlanParams, ChangePlanResult } from './models';

// Context and hooks
export { getStripePublishableKey, isStripeEnabled, loadStripeOnce, setStripeContext, type StripeContext, tryUseStripe, useStripe } from './stripe.svelte';
export { handleUpgradeRequired, isUpgradeRequired } from './upgrade-required.svelte';
