import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

const showUpgradeDialog = vi.hoisted(() => vi.fn());
vi.mock('$features/billing/upgrade-required.svelte', () => ({ showUpgradeDialog }));

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

        expect(showUpgradeDialog).toHaveBeenCalledWith('organization-id', 'Exie is available on Medium plans and higher.', {
            directToPlanPicker: true,
            initialTierId: 'EX_MEDIUM',
            onSuccess: undefined
        });
    });

    it('does not offer an upgrade without an organization', () => {
        render(AssistantUpgradeRequired, { props: { message: 'Select an organization to use Exie.' } });

        expect(screen.queryByRole('button', { name: 'View upgrade options' })).toBeNull();
    });

    it('shows a retry action when access loading fails', async () => {
        const onRetry = vi.fn();
        render(AssistantUpgradeRequired, { props: { accessState: 'error', onRetry } });

        await fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

        expect(onRetry).toHaveBeenCalledOnce();
    });
});
