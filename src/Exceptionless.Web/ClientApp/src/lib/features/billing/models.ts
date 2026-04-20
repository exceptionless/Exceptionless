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
 * Keys match the snake_case convention of the Exceptionless API.
 */
export interface ChangePlanParams {
    /** Optional coupon code to apply */
    coupon_id?: string;
    /** Last 4 digits of the card (for display purposes) */
    last4?: string;
    /** The plan ID to change to */
    plan_id: string;
    /** Stripe PaymentMethod ID or legacy token */
    stripe_token?: string;
}
