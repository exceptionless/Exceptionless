/**
 * Billing models - re-exports from generated types plus billing-specific types.
 */

export type { BillingPlan, ChangePlanRequest, ChangePlanResult } from '$lib/generated/api';

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
