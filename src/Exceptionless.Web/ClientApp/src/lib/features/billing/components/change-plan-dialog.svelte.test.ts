import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import type { ViewOrganization } from '#features/organizations/models.ts';
import type { BillingPlan } from '#generated/api.ts';

const plans: BillingPlan[] = [
    {
        description: 'Free',
        has_premium_features: false,
        id: 'EX_FREE',
        is_hidden: false,
        max_events_per_month: 3000,
        max_projects: -1,
        max_users: -1,
        name: 'Free',
        price: 0,
        retention_days: 3
    },
    {
        description: 'Small',
        has_premium_features: true,
        id: 'EX_SMALL',
        is_hidden: false,
        max_events_per_month: 15000,
        max_projects: -1,
        max_users: -1,
        name: 'Small',
        price: 15,
        retention_days: 30
    },
    {
        description: 'Small yearly',
        has_premium_features: true,
        id: 'EX_SMALL_YEARLY',
        is_hidden: false,
        max_events_per_month: 15000,
        max_projects: -1,
        max_users: -1,
        name: 'Small (Yearly)',
        price: 165,
        retention_days: 30
    },
    {
        description: 'Medium',
        has_premium_features: true,
        id: 'EX_MEDIUM',
        is_hidden: false,
        max_events_per_month: 75000,
        max_projects: -1,
        max_users: -1,
        name: 'Medium',
        price: 49,
        retention_days: 90
    }
];

vi.mock('#features/organizations/api.svelte.ts', () => ({
    changePlanMutation: () => ({ mutateAsync: vi.fn() }),
    getPlansQuery: () => ({ data: plans, error: null, isLoading: false })
}));
vi.mock('#features/billing/stripe.svelte.ts', async (importOriginal) => ({
    ...(await importOriginal<typeof import('#features/billing/stripe.svelte.ts')>()),
    isStripeEnabled: () => true,
    loadStripeOnce: async () => null
}));

import ChangePlanDialog from './change-plan-dialog.svelte';

describe('ChangePlanDialog loss warning', () => {
    it('warns for trial to Small in both intervals without blocking confirmation, then clears for Medium', async () => {
        // Arrange
        const organization = {
            billing_price: 0,
            card_last4: '4242',
            has_premium_features: true,
            id: 'trial-organization',
            max_events_per_month: 75000,
            name: 'Trial organization',
            plan_id: 'EX_TRIAL',
            plan_name: 'Trial',
            retention_days: 90,
            trial_ends_utc: '2099-01-01T00:00:00Z'
        } as ViewOrganization;

        // Act
        render(ChangePlanDialog, { initialPlanId: 'EX_SMALL', onclose: vi.fn(), organization });

        // Assert
        const losses = await screen.findByTestId('downgrade-losses');
        expect(losses).toHaveTextContent('event history drops from 90 days to 30 days');
        expect(losses).toHaveTextContent('limit drops to 15,000 events a month');
        expect(screen.getByRole('button', { name: 'Start Small yearly' })).toBeEnabled();

        // Act
        await fireEvent.click(screen.getByRole('tab', { name: 'Monthly' }));

        // Assert
        expect(losses).toHaveTextContent('event history drops from 90 days to 30 days');
        expect(screen.getByRole('button', { name: 'Start Small monthly' })).toBeEnabled();

        // Act
        await fireEvent.click(screen.getByRole('button', { name: /^Medium/ }));

        // Assert
        expect(screen.queryByTestId('downgrade-losses')).toBeNull();
        expect(screen.getByRole('button', { name: 'Start Medium monthly' })).toBeEnabled();
    });
});
