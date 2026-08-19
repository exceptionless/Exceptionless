import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

const showChangePlanDialog = vi.hoisted(() => vi.fn());
const isStripeEnabled = vi.hoisted(() => vi.fn(() => true));
vi.mock('$features/billing/change-plan.svelte', () => ({ showChangePlanDialog }));
vi.mock('$features/billing/stripe.svelte', () => ({ isStripeEnabled }));

import AssistantUpgradeRequired from './assistant-upgrade-required.svelte';

describe('AssistantUpgradeRequired', () => {
    it('explains the plan requirement and opens the upgrade flow', async () => {
        render(AssistantUpgradeRequired, {
            props: {
                accessState: 'upgrade-required',
                message: 'Exie is available on Medium plans and higher.',
                minimumPlanId: 'EX_MEDIUM',
                organizationId: 'organization-id'
            }
        });

        expect(screen.getByText('Exie is available on Medium plans and higher.')).toBeTruthy();

        await fireEvent.click(screen.getByRole('button', { name: 'Upgrade Plan' }));

        expect(showChangePlanDialog).toHaveBeenCalledWith('organization-id', {
            initialPlanId: 'EX_MEDIUM',
            onSuccess: undefined
        });
    });

    it('does not offer an upgrade without an organization', () => {
        render(AssistantUpgradeRequired, { props: { message: 'Select an organization to use Exie.' } });

        expect(screen.queryByRole('button', { name: 'View upgrade options' })).toBeNull();
    });

    it('does not offer an unusable upgrade when billing is disabled', () => {
        isStripeEnabled.mockReturnValueOnce(false);
        render(AssistantUpgradeRequired, {
            props: {
                accessState: 'upgrade-required',
                organizationId: 'organization-id'
            }
        });

        expect(screen.queryByRole('button', { name: 'Upgrade Plan' })).toBeNull();
        expect(screen.getByText('Billing checkout is unavailable in this environment.')).toBeTruthy();
    });

    it('shows a retry action when access loading fails', async () => {
        const onRetry = vi.fn();
        render(AssistantUpgradeRequired, { props: { accessState: 'error', onRetry } });

        await fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

        expect(onRetry).toHaveBeenCalledOnce();
    });
});
