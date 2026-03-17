/**
 * Billing models - re-exports from generated types plus billing-specific types.
 */

export type { BillingPlan, ChangePlanResult } from '$lib/generated/api';

/**
 * Card mode for the payment form.
 */
export type CardMode = 'existing' | 'new';

/**
 * State for the change plan form.
 */
export interface ChangePlanFormState {
    cardMode: CardMode;
    couponId: string;
    selectedPlanId: null | string;
}

/**
 * Parameters for the change-plan API call.
 */
export interface ChangePlanParams {
    /** Optional coupon code to apply */
    couponId?: string;
    /** Last 4 digits of the card (for display purposes) */
    last4?: string;
    /** The plan ID to change to */
    planId: string;
    /** Stripe PaymentMethod ID or legacy token */
    stripeToken?: string;
}
