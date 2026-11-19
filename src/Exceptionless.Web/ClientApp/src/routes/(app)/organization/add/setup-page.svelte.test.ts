import { ProblemDetails } from '@foundatiofx/fetchclient';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SetupPage from './+page.svelte';

const mocks = vi.hoisted(() => ({
    goto: vi.fn(),
    organization: { current: undefined as string | undefined },
    postOrganization: vi.fn(),
    postProject: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({
    resolve: (path: string, params?: Record<string, string>) =>
        Object.entries(params ?? {}).reduce((href, [key, value]) => href.replace(`[${key}]`, value), path.replace('/(app)', ''))
}));
vi.mock('#features/billing/index.ts', () => ({ showBillingDialogOnUpgradeProblem: () => false }));
vi.mock('#features/organizations/api.svelte.ts', () => ({
    getOrganizationsQuery: () => ({ data: { data: [] } }),
    postOrganization: () => ({ mutateAsync: mocks.postOrganization })
}));
vi.mock('#features/organizations/context.svelte.ts', () => ({ organization: mocks.organization }));
vi.mock('#features/organizations/hooks/use-hide-organization-notifications.svelte.ts', () => ({ useHideOrganizationNotifications: vi.fn() }));
vi.mock('#features/product-tours/actions.svelte.ts', () => ({ createProductTourActions: () => ({ dismiss: vi.fn() }) }));
vi.mock('#features/product-tours/state.svelte.ts', () => ({ productTourCheckpoint: { advance: vi.fn(), current: undefined } }));
vi.mock('#features/projects/api.svelte.ts', () => ({ postProject: () => ({ mutateAsync: mocks.postProject }) }));
vi.mock('#features/users/api.svelte.ts', () => ({
    getMeQuery: () => ({ data: { email_address: 'jane@acme.com', full_name: 'Jane Doe' } })
}));

describe('organization setup', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mocks.organization.current = undefined;
    });

    it('suggests the organization from the email domain', async () => {
        // Act
        render(SetupPage);

        // Assert
        await waitFor(() => expect(screen.getByLabelText('Organization name')).toHaveValue('Acme'));
    });

    it('rejects a whitespace-only app name without creating anything', async () => {
        // Arrange
        render(SetupPage);
        await fireEvent.input(screen.getByLabelText("What's your app called?"), { target: { value: '   ' } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

        // Assert
        expect(await screen.findByText('Give your app a name')).toBeVisible();
        expect(mocks.postOrganization).not.toHaveBeenCalled();
    });

    it('reuses the created organization when retrying a failed project', async () => {
        // Arrange
        const failure = new ProblemDetails();
        failure.title = 'Project could not be created';
        mocks.postOrganization.mockResolvedValue({ id: 'organization-1', name: 'Acme' });
        mocks.postProject.mockRejectedValueOnce(failure).mockResolvedValueOnce({ id: 'project-1' });
        render(SetupPage);
        await fireEvent.input(screen.getByLabelText("What's your app called?"), { target: { value: '  Storefront API ' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
        await waitFor(() => expect(mocks.postProject).toHaveBeenCalledTimes(1));
        await waitFor(() => expect(screen.getByRole('button', { name: 'Continue' })).toBeEnabled());

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

        // Assert
        await waitFor(() => expect(mocks.goto).toHaveBeenCalledWith('/project/project-1/configure?redirect=true'));
        expect(mocks.postOrganization).toHaveBeenCalledExactlyOnceWith({ name: 'Acme' });
        expect(mocks.postProject).toHaveBeenCalledTimes(2);
        expect(mocks.postProject).toHaveBeenLastCalledWith(expect.objectContaining({ name: 'Storefront API', organization_id: 'organization-1' }));
        expect(screen.getByLabelText('Organization name')).toHaveAttribute('readonly');
    });
});
