import type { ViewOrganization } from '$features/organizations/models';
import type { BillingPlan } from '$lib/generated/api';
import type { Meta, StoryObj } from '@storybook/sveltekit';

import Harness from './change-plan-dialog-harness.svelte';

const MOCK_PLANS: BillingPlan[] = [
    {
        description: 'Free',
        has_premium_features: false,
        id: 'EX_FREE',
        is_hidden: false,
        max_events_per_month: 3000,
        max_projects: 1,
        max_users: 1,
        name: 'Free',
        price: 0,
        retention_days: 3
    },
    {
        description: 'Small ($15/month)',
        has_premium_features: true,
        id: 'EX_SMALL',
        is_hidden: false,
        max_events_per_month: 15000,
        max_projects: 5,
        max_users: 10,
        name: 'Small',
        price: 15,
        retention_days: 30
    },
    {
        description: 'Small Yearly ($165/year - Save $15)',
        has_premium_features: true,
        id: 'EX_SMALL_YEARLY',
        is_hidden: false,
        max_events_per_month: 15000,
        max_projects: 5,
        max_users: 10,
        name: 'Small (Yearly)',
        price: 165,
        retention_days: 30
    },
    {
        description: 'Medium ($49/month)',
        has_premium_features: true,
        id: 'EX_MEDIUM',
        is_hidden: false,
        max_events_per_month: 75000,
        max_projects: 15,
        max_users: 25,
        name: 'Medium',
        price: 49,
        retention_days: 90
    },
    {
        description: 'Medium Yearly ($539/year - Save $49)',
        has_premium_features: true,
        id: 'EX_MEDIUM_YEARLY',
        is_hidden: false,
        max_events_per_month: 75000,
        max_projects: 15,
        max_users: 25,
        name: 'Medium (Yearly)',
        price: 539,
        retention_days: 90
    },
    {
        description: 'Large ($99/month)',
        has_premium_features: true,
        id: 'EX_LARGE',
        is_hidden: false,
        max_events_per_month: 250000,
        max_projects: -1,
        max_users: -1,
        name: 'Large',
        price: 99,
        retention_days: 180
    },
    {
        description: 'Large Yearly ($1,089/year - Save $99)',
        has_premium_features: true,
        id: 'EX_LARGE_YEARLY',
        is_hidden: false,
        max_events_per_month: 250000,
        max_projects: -1,
        max_users: -1,
        name: 'Large (Yearly)',
        price: 1089,
        retention_days: 180
    },
    {
        description: 'Extra Large ($199/month)',
        has_premium_features: true,
        id: 'EX_XL',
        is_hidden: false,
        max_events_per_month: 1000000,
        max_projects: -1,
        max_users: -1,
        name: 'Extra Large',
        price: 199,
        retention_days: 180
    },
    {
        description: 'Extra Large Yearly ($2,189/year - Save $199)',
        has_premium_features: true,
        id: 'EX_XL_YEARLY',
        is_hidden: false,
        max_events_per_month: 1000000,
        max_projects: -1,
        max_users: -1,
        name: 'Extra Large (Yearly)',
        price: 2189,
        retention_days: 180
    },
    {
        description: 'Enterprise ($499/month)',
        has_premium_features: true,
        id: 'EX_ENT',
        is_hidden: false,
        max_events_per_month: 3000000,
        max_projects: -1,
        max_users: -1,
        name: 'Enterprise',
        price: 499,
        retention_days: 180
    },
    {
        description: 'Enterprise Yearly ($5,489/year - Save $499)',
        has_premium_features: true,
        id: 'EX_ENT_YEARLY',
        is_hidden: false,
        max_events_per_month: 3000000,
        max_projects: -1,
        max_users: -1,
        name: 'Enterprise (Yearly)',
        price: 5489,
        retention_days: 180
    }
];

/** Helper to build a ViewOrganization with sensible defaults. */
function makeOrg(overrides: Partial<ViewOrganization> = {}): ViewOrganization {
    return {
        billing_change_date: null,
        billing_changed_by_user_id: null,
        billing_price: 0,
        billing_status: 0 as never,
        bonus_events_per_month: 0,
        bonus_expiration: null,
        card_last4: null,
        created_utc: '2024-01-15T00:00:00Z',
        data: null,
        event_count: 427,
        has_premium_features: false,
        id: '507f1f77bcf86cd799439011',
        invites: [],
        is_over_monthly_limit: false,
        is_over_request_limit: false,
        is_suspended: false,
        is_throttled: false,
        max_events_per_month: 3000,
        max_projects: 1,
        max_users: 1,
        name: 'Acme Corp',
        plan_description: 'Free plan',
        plan_id: 'EX_FREE',
        plan_name: 'Free',
        project_count: 1,
        retention_days: 3,
        stack_count: 12,
        subscribe_date: null,
        suspension_code: null,
        suspension_date: null,
        suspension_notes: null,
        updated_utc: '2025-04-10T00:00:00Z',
        usage: [],
        usage_hours: [],
        ...overrides
    };
}

const meta = {
    component: Harness,
    parameters: {
        layout: 'centered'
    },
    tags: ['autodocs'],
    title: 'Features/Billing/ChangePlanDialog'
} satisfies Meta<typeof Harness>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Plans failed to load — shows error message. */
export const ErrorLoadingPlans: Story = {
    args: {
        organization: makeOrg(),
        plans: [] as BillingPlan[]
    },
    name: 'Error loading plans'
};

/** Free-plan org, dialog open — upsells to the first paid tier (Small). */
export const Default: Story = {
    args: {
        organization: makeOrg(),
        plans: MOCK_PLANS
    }
};

/** Paid Small monthly org selects a different paid plan (Large monthly). */
export const ChangePlan: Story = {
    args: {
        organization: makeOrg({
            billing_price: 15,
            card_last4: '4242',
            has_premium_features: true,
            max_events_per_month: 15000,
            max_projects: 5,
            max_users: 10,
            plan_id: 'EX_SMALL',
            plan_name: 'Small',
            retention_days: 30,
            subscribe_date: '2024-06-01T00:00:00Z'
        }),
        plans: MOCK_PLANS
    },
    name: 'Change plan'
};

/** Small monthly org switches to yearly billing (same tier). */
export const IntervalSwitch: Story = {
    args: {
        organization: makeOrg({
            billing_price: 15,
            card_last4: '4242',
            has_premium_features: true,
            max_events_per_month: 15000,
            max_projects: 5,
            max_users: 10,
            plan_id: 'EX_SMALL',
            plan_name: 'Small',
            retention_days: 30,
            subscribe_date: '2024-06-01T00:00:00Z'
        }),
        plans: MOCK_PLANS
    },
    name: 'Interval switch (Small yearly)'
};

/** Paid org keeps current plan but wants to update their payment method. */
export const UpdateCardOnly: Story = {
    args: {
        organization: makeOrg({
            billing_price: 49,
            card_last4: '1234',
            has_premium_features: true,
            max_events_per_month: 75000,
            max_projects: 15,
            max_users: 25,
            plan_id: 'EX_MEDIUM',
            plan_name: 'Medium',
            retention_days: 90,
            subscribe_date: '2024-03-01T00:00:00Z'
        }),
        plans: MOCK_PLANS
    },
    name: 'Update card only'
};

/** Paid org keeps current plan and wants to apply a coupon. */
export const ApplyCouponOnly: Story = {
    args: {
        organization: makeOrg({
            billing_price: 99,
            card_last4: '5678',
            has_premium_features: true,
            max_events_per_month: 250000,
            max_projects: -1,
            max_users: -1,
            plan_id: 'EX_LARGE',
            plan_name: 'Large',
            retention_days: 180,
            subscribe_date: '2024-01-15T00:00:00Z'
        }),
        plans: MOCK_PLANS
    },
    name: 'Apply coupon only'
};

/** Paid org changes plan, updates card, and applies a coupon — all at once. */
export const PlanCardCoupon: Story = {
    args: {
        organization: makeOrg({
            billing_price: 15,
            card_last4: '9999',
            has_premium_features: true,
            max_events_per_month: 15000,
            max_projects: 5,
            max_users: 10,
            plan_id: 'EX_SMALL',
            plan_name: 'Small',
            retention_days: 30,
            subscribe_date: '2024-09-01T00:00:00Z'
        }),
        plans: MOCK_PLANS
    },
    name: 'Plan + card + coupon'
};

/** Free-plan org upgrading to a paid plan for the first time (no card on file). */
export const FirstTimePaid: Story = {
    args: {
        organization: makeOrg({
            billing_price: 0,
            card_last4: null,
            plan_id: 'EX_FREE',
            plan_name: 'Free'
        }),
        plans: MOCK_PLANS
    },
    name: 'First-time paid'
};

/** Paid org selecting the Free plan to downgrade. */
export const DowngradeToFree: Story = {
    args: {
        organization: makeOrg({
            billing_price: 199,
            card_last4: '4242',
            has_premium_features: true,
            max_events_per_month: 1000000,
            max_projects: -1,
            max_users: -1,
            plan_id: 'EX_XL',
            plan_name: 'Extra Large',
            retention_days: 180,
            subscribe_date: '2023-11-01T00:00:00Z'
        }),
        plans: MOCK_PLANS
    },
    name: 'Downgrade to Free'
};
