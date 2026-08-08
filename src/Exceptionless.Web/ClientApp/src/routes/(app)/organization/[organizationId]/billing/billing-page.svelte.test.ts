import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

const { deleteOrganizationData, postOrganizationData } = vi.hoisted(() => ({
    deleteOrganizationData: vi.fn(async () => true),
    postOrganizationData: vi.fn(async () => true)
}));

vi.mock('$app/paths', () => ({
    resolve: (path: string) => path
}));

vi.mock('$app/state', () => ({
    page: { params: { organizationId: 'organization-id' } }
}));

vi.mock('$env/dynamic/public', () => ({
    env: { PUBLIC_STRIPE_PUBLISHABLE_KEY: '' }
}));

vi.mock('$features/organizations/api.svelte', () => ({
    deleteOrganizationDataMutation: () => ({ mutateAsync: deleteOrganizationData }),
    getInvoicesQuery: () => ({ data: { data: [] }, error: null, isLoading: false }),
    getOrganizationQuery: () => ({
        data: { data: {}, id: 'organization-id', plan_name: 'Free' },
        error: null,
        isLoading: false,
        isSuccess: true
    }),
    postOrganizationDataMutation: () => ({ mutateAsync: postOrganizationData })
}));

vi.mock('kit-query-params', () => ({
    queryParamsState: () => ({ changePlan: false })
}));

vi.mock('svelte-sonner', () => ({
    toast: {
        dismiss: vi.fn(),
        error: vi.fn(),
        success: vi.fn()
    }
}));

import BillingPage from './+page.svelte';

afterEach(() => {
    vi.clearAllMocks();
});

describe('Billing page', () => {
    it('autosaves billing information after initializing the form', async () => {
        render(BillingPage);

        await fireEvent.input(screen.getByLabelText('Billing name'), { target: { value: 'Acme, Inc.' } });

        await waitFor(
            () => {
                expect(postOrganizationData).toHaveBeenCalledWith({
                    key: 'billing_name',
                    organizationId: 'organization-id',
                    value: 'Acme, Inc.'
                });
            },
            { timeout: 2000 }
        );
        expect(deleteOrganizationData).not.toHaveBeenCalled();
    });
});
