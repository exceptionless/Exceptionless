import type { AssistantAccess } from '$features/assistant/models';
import type { ViewCurrentUser } from '$features/users/models';

import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { productTourCheckpoint } from '../state.svelte';
import ProductTourHost from './product-tour-host.svelte';

const mocks = vi.hoisted(() => ({
    invalidateAssistantAccessQueries: vi.fn(),
    isStripeEnabled: vi.fn(),
    mutateAsync: vi.fn(),
    queryClient: {},
    showChangePlanDialog: vi.fn()
}));

vi.mock('$app/state', () => ({ page: { route: { id: '/(app)/stack/all' } } }));
vi.mock('$features/assistant/api.svelte', () => ({ invalidateAssistantAccessQueries: mocks.invalidateAssistantAccessQueries }));
vi.mock('$features/billing/change-plan.svelte', () => ({ showChangePlanDialog: mocks.showChangePlanDialog }));
vi.mock('$features/billing/stripe.svelte', () => ({ isStripeEnabled: mocks.isStripeEnabled }));
vi.mock('$features/events/api.svelte', () => ({ getOrganizationEventsQuery: () => ({ data: { data: [] }, isError: false, isPending: false }) }));
vi.mock('$features/projects/api.svelte', () => ({ getOrganizationProjectsQuery: () => ({ data: { data: [] }, isError: false, isSuccess: true }) }));
vi.mock('$features/users/api.svelte', () => ({ putCurrentUserProductTour: () => ({ mutateAsync: mocks.mutateAsync }) }));
vi.mock('@tanstack/svelte-query', () => ({ useQueryClient: () => mocks.queryClient }));
vi.mock('../actions.svelte', () => ({ createProductTourActions: () => ({ complete: vi.fn() }) }));

const assistantAccess: AssistantAccess = { enabled: true, has_access: false, minimum_plan_id: 'EX_MEDIUM', upgrade_required: true };
const currentUser = { id: 'user', product_tours: { app_welcome: '2026-09-08T00:00:00Z' } } as ViewCurrentUser;

function props() {
    return {
        assistantAccess,
        closeOverlays: vi.fn(),
        currentUser,
        isAnyOverlayOpen: false,
        isImpersonating: false,
        isMobile: false,
        isSetupPage: false,
        openAssistant: vi.fn(async () => {}),
        organizationId: 'organization',
        pathname: '/next/stack/all',
        setMobileNavigationOpen: vi.fn(),
        stateSettled: true
    };
}

describe('ProductTourHost', () => {
    beforeEach(() => {
        vi.resetAllMocks();
        mocks.isStripeEnabled.mockReturnValue(true);
        mocks.mutateAsync.mockResolvedValue({ recorded_utc: '2026-09-09T00:00:00Z' });
    });

    afterEach(() => {
        cleanup();
        productTourCheckpoint.clear();
    });

    it('opens Change Plan directly with the eligible plan and refreshes access after success', async () => {
        // Arrange
        const options = props();
        render(ProductTourHost, options);

        // Act
        await fireEvent.click(await screen.findByRole('button', { name: 'Upgrade Plan' }));

        // Assert
        expect(mocks.showChangePlanDialog).toHaveBeenCalledExactlyOnceWith('organization', {
            initialPlanId: 'EX_MEDIUM',
            onSuccess: expect.any(Function)
        });
        expect(options.openAssistant).not.toHaveBeenCalled();
        expect(mocks.mutateAsync).toHaveBeenCalledWith({ tourName: 'exie-announcement', userId: 'user' });

        // Act
        await mocks.showChangePlanDialog.mock.calls[0]![1].onSuccess();

        // Assert
        expect(mocks.invalidateAssistantAccessQueries).toHaveBeenCalledExactlyOnceWith(mocks.queryClient);
    });

    it.each([
        { billingEnabled: false, upgradeRequired: true },
        { billingEnabled: true, upgradeRequired: false }
    ])('opens Exie without promising an unavailable upgrade: %j', async ({ billingEnabled, upgradeRequired }) => {
        // Arrange
        mocks.isStripeEnabled.mockReturnValue(billingEnabled);
        const options = props();
        options.assistantAccess = { ...assistantAccess, upgrade_required: upgradeRequired };
        render(ProductTourHost, options);

        // Act
        await fireEvent.click(await screen.findByRole('button', { name: 'Open Exie' }));

        // Assert
        expect(options.openAssistant).toHaveBeenCalledOnce();
        expect(mocks.showChangePlanDialog).not.toHaveBeenCalled();
        expect(screen.queryByRole('button', { name: 'Upgrade Plan' })).toBeNull();
    });

    it('selects invitations for the new user after the previous user dismisses one', async () => {
        // Arrange
        const options = props();
        const view = render(ProductTourHost, options);
        await fireEvent.click(await screen.findByRole('button', { name: 'Dismiss Exie announcement' }));
        expect(screen.queryByRole('button', { name: 'Upgrade Plan' })).toBeNull();

        // Act
        await view.rerender({ currentUser: { ...currentUser, id: 'next-user' } });

        // Assert
        expect(await screen.findByRole('button', { name: 'Upgrade Plan' })).toBeTruthy();
    });

    it('clears an active guide when its organization changes', async () => {
        // Arrange
        const options = props();
        productTourCheckpoint.start('saved-view-create', 'open-view-menu', 'user', 'organization');
        const view = render(ProductTourHost, options);

        // Act
        await view.rerender({ organizationId: 'other-organization' });

        // Assert
        expect(productTourCheckpoint.current).toBeUndefined();
    });
});
