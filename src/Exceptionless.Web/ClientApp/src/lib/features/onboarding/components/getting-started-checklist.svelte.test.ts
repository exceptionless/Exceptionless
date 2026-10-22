import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import GettingStartedChecklist from './getting-started-checklist.svelte';

const recordTour = vi.hoisted(() => vi.fn());
const toastMock = vi.hoisted(() => ({ error: vi.fn(), success: vi.fn() }));
const meState = vi.hoisted(() => ({
    current: undefined as
        | undefined
        | {
              email_address: string;
              id: string;
              is_email_address_verified: boolean;
              product_tours: Record<string, string>;
          }
}));

vi.mock('$app/paths', () => ({
    resolve: (path: string, params?: Record<string, string>) =>
        Object.entries(params ?? {}).reduce((href, [key, value]) => href.replace(`[${key}]`, value), path.replace('/(app)', ''))
}));

vi.mock('svelte-sonner', () => ({ toast: toastMock }));

vi.mock('#features/organizations/api.svelte.ts', () => ({
    getOrganizationQuery: () => ({ data: { created_utc: new Date().toISOString(), id: 'organization-id', plan_id: 'EX_MEDIUM' } })
}));

vi.mock('#features/organizations/context.svelte.ts', () => ({
    organization: { current: 'organization-id' }
}));

vi.mock('../onboarding-state.svelte', () => ({
    useOrganizationOnboarding: () => ({ continueSetupHref: '/project/project-id/configure', hasProject: true, hasReceivedEvents: true, isReady: true })
}));

vi.mock('#features/users/api.svelte.ts', () => ({
    getMeQuery: () => ({
        get data() {
            return meState.current;
        }
    }),
    putCurrentUserProductTour: () => ({ mutateAsync: recordTour }),
    resendVerificationEmail: () => ({ isPending: false, mutateAsync: vi.fn() })
}));

// Each test uses its own user so the once-per-session celebration guard doesn't leak between tests.
let userNumber = 0;

function setUser(productTours: Record<string, string>, emailVerified = true): string {
    userNumber += 1;
    const id = `user-${userNumber}`;
    meState.current = { email_address: 'riley@example.com', id, is_email_address_verified: emailVerified, product_tours: productTours };
    return id;
}

const recordedAt = '2026-10-22T00:00:00.000Z';
const allVisitsExceptVersions = {
    checklist_ai_tools: recordedAt,
    checklist_invite_team: recordedAt,
    checklist_notifications: recordedAt
};

describe('GettingStartedChecklist', () => {
    beforeEach(() => {
        localStorage.clear();
        recordTour.mockReset().mockResolvedValue({ recorded_utc: recordedAt });
        toastMock.error.mockReset();
        toastMock.success.mockReset();
    });

    it('retires itself and celebrates when every item is already done', async () => {
        // Arrange
        const userId = setUser({ ...allVisitsExceptVersions, checklist_versions: recordedAt });

        // Act
        render(GettingStartedChecklist);

        // Assert
        await waitFor(() => expect(recordTour).toHaveBeenCalledWith({ tourName: 'checklist-dismissed', userId }));
        expect(screen.queryByRole('region', { name: "Exie's checklist" })).not.toBeInTheDocument();
        expect(toastMock.success).toHaveBeenCalledWith("You finished Exie's checklist!", expect.anything());
    });

    it('retires itself when the user opens the last item', async () => {
        // Arrange
        localStorage.setItem(`getting-started-checklist-expanded:${setUser(allVisitsExceptVersions)}`, 'true');
        render(GettingStartedChecklist);
        const lastItem = screen.getByRole('link', { name: /Track versions & environments/ });

        // Act
        await fireEvent.click(lastItem);

        // Assert
        await waitFor(() => expect(screen.queryByRole('region', { name: "Exie's checklist" })).not.toBeInTheDocument());
        expect(recordTour).toHaveBeenCalledWith(expect.objectContaining({ tourName: 'checklist-versions' }));
        expect(recordTour).toHaveBeenCalledWith(expect.objectContaining({ tourName: 'checklist-dismissed' }));
        expect(toastMock.success).toHaveBeenCalledOnce();
    });

    it('keeps the clicked link in place until the router has read it', async () => {
        // Arrange
        setUser({});
        render(GettingStartedChecklist);
        const nextLink = screen.getByRole('link', { name: 'Review your notifications' });

        // Act
        await fireEvent.click(nextLink);
        await tick();

        // Assert
        expect(nextLink).toBeInTheDocument();
        expect(nextLink).toHaveAttribute('href', '/account/notifications');
        await waitFor(() => expect(screen.getByRole('link', { name: 'Connect your AI assistant via MCP' })).toBeInTheDocument());
    });

    it('stays up while items remain', () => {
        // Arrange
        setUser({}, false);

        // Act
        render(GettingStartedChecklist);

        // Assert
        expect(screen.getByRole('region', { name: "Exie's checklist" })).toBeInTheDocument();
        expect(recordTour).not.toHaveBeenCalled();
    });

    it('hides for good when dismissed', async () => {
        // Arrange
        const userId = setUser({});
        render(GettingStartedChecklist);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Dismiss checklist' }));

        // Assert
        expect(screen.queryByRole('region', { name: "Exie's checklist" })).not.toBeInTheDocument();
        expect(recordTour).toHaveBeenCalledWith({ tourName: 'checklist-dismissed', userId });
        expect(toastMock.success).not.toHaveBeenCalled();
    });

    it('stays hidden once the dismissal is recorded', () => {
        // Arrange
        setUser({ checklist_dismissed: recordedAt });

        // Act
        render(GettingStartedChecklist);

        // Assert
        expect(screen.queryByRole('region', { name: "Exie's checklist" })).not.toBeInTheDocument();
        expect(recordTour).not.toHaveBeenCalled();
    });

    it('comes back with an error when the dismissal cannot be saved', async () => {
        // Arrange
        setUser({});
        recordTour.mockRejectedValue(new Error('offline'));
        render(GettingStartedChecklist);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Dismiss checklist' }));

        // Assert
        expect(await screen.findByRole('region', { name: "Exie's checklist" })).toBeInTheDocument();
        expect(toastMock.error).toHaveBeenCalledWith('We could not hide the checklist. Please try again.');
    });
});
