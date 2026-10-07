import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { deleteOrganizationData, organization, postOrganizationData } = vi.hoisted(() => ({
    deleteOrganizationData: vi.fn(async () => true),
    organization: { data: {} as Record<string, string>, id: 'organization-id', plan_name: 'Free' },
    postOrganizationData: vi.fn<(variables: { key: string; organizationId: string; value: string }) => Promise<boolean>>()
}));

vi.mock('$app/paths', () => ({
    resolve: (path: string) => path
}));

vi.mock('$app/navigation', () => ({
    beforeNavigate: vi.fn()
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
        data: organization,
        error: null,
        isLoading: false,
        isSuccess: true
    }),
    postOrganizationDataMutation: () => ({ mutateAsync: postOrganizationData })
}));

vi.mock('$shared/query-params', () => ({
    createQueryParameters: () => ({ changePlan: false })
}));

vi.mock('svelte-sonner', () => ({
    toast: {
        dismiss: vi.fn(),
        error: vi.fn(),
        info: vi.fn(),
        success: vi.fn()
    }
}));

import BillingPage from './+page.svelte';

beforeEach(() => {
    organization.data = {};
    postOrganizationData.mockImplementation(async ({ key, value }) => {
        organization.data[key] = value;
        return true;
    });
});

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

    it('saves edits made while a previous submission is pending', async () => {
        const firstWrite = Promise.withResolvers<void>();
        postOrganizationData.mockImplementationOnce(async ({ key, value }) => {
            await firstWrite.promise;
            organization.data[key] = value;
            return true;
        });
        render(BillingPage);
        const name = screen.getByLabelText('Billing name');
        const form = name.closest('form')!;

        await fireEvent.input(name, { target: { value: 'First name' } });
        await fireEvent.submit(form);
        await waitFor(() => expect(postOrganizationData).toHaveBeenCalledTimes(1));

        await fireEvent.input(name, { target: { value: 'Latest name' } });
        await fireEvent.submit(form);
        expect(postOrganizationData).toHaveBeenCalledTimes(1);
        firstWrite.resolve();

        await waitFor(() => expect(organization.data.billing_name).toBe('Latest name'));
        expect((name as HTMLInputElement).value).toBe('Latest name');
        expect(postOrganizationData).toHaveBeenLastCalledWith({ key: 'billing_name', organizationId: 'organization-id', value: 'Latest name' });
    });

    it('retains input after a failed save and lets the next edit retry', async () => {
        postOrganizationData.mockRejectedValueOnce(new Error('Save failed'));
        render(BillingPage);
        const name = screen.getByLabelText('Billing name');
        const form = name.closest('form')!;

        await fireEvent.input(name, { target: { value: 'Unsaved name' } });
        await fireEvent.submit(form);
        await waitFor(() => expect(screen.getByText(/Error saving billing information/)).toBeTruthy());
        expect((name as HTMLInputElement).value).toBe('Unsaved name');
        expect(organization.data.billing_name).toBeUndefined();

        await fireEvent.input(name, { target: { value: 'Retried name' } });
        await fireEvent.submit(form);
        await waitFor(() => expect(organization.data.billing_name).toBe('Retried name'));
        await waitFor(() => expect(screen.queryByText(/Error saving billing information/)).toBeNull());
    });
});
