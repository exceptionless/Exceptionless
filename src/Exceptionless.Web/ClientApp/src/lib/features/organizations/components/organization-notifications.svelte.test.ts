import { fireEvent, render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { ViewOrganization } from '#features/organizations/models.ts';

import { dismissOrganizationNotification } from '#features/organizations/notification-dismissals.ts';
import { ChangeType } from '#features/websockets/models.ts';

import OrganizationNotifications, { recordConfiguredProjectId } from './organization-notifications.svelte';
import { notificationTestState } from './organization-notifications.test-fake.svelte';

const organizationRefetch = vi.hoisted(() => vi.fn());
const projectsRefetch = vi.hoisted(() => vi.fn());
const projects = vi.hoisted(() => [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }]);
const recordProductTour = vi.hoisted(() => vi.fn());
const invalidateQueries = vi.hoisted(() => vi.fn());
const state = notificationTestState;

vi.mock('@tanstack/svelte-query', async (importOriginal) => {
    const original = await importOriginal<typeof import('@tanstack/svelte-query')>();
    return {
        ...original,
        useQueryClient: () => ({ invalidateQueries })
    };
});

vi.mock('#features/organizations/api.svelte.ts', async () => {
    const { notificationTestState } = await import('./organization-notifications.test-fake.svelte');
    return {
        getOrganizationQuery: () => ({
            get data() {
                return notificationTestState.organization;
            },
            refetch: organizationRefetch
        }),
        getOrganizationsQuery: () => ({ data: { data: [] } })
    };
});

vi.mock('#features/organizations/context.svelte.ts', () => ({
    organization: { current: 'organization-id' }
}));

vi.mock('#features/projects/api.svelte.ts', () => ({
    getOrganizationProjectsQuery: () => ({ data: { data: projects }, isSuccess: true, refetch: projectsRefetch })
}));

vi.mock('#features/users/api.svelte.ts', async () => {
    const { notificationTestState } = await import('./organization-notifications.test-fake.svelte');
    return {
        getMeQuery: () => ({
            get data() {
                return notificationTestState.me;
            }
        }),
        putCurrentUserProductTour: () => ({ mutate: recordProductTour }),
        queryKeys: { me: () => ['User', 'me'] },
        setCurrentUserProductTourRecorded: (_: unknown, tourName: string) => {
            notificationTestState.me.product_tours = { ...notificationTestState.me.product_tours, [tourName.replaceAll('-', '_')]: '2026-10-23T00:00:00Z' };
        }
    };
});

const DAY = 24 * 60 * 60 * 1000;

function createOrganization(overrides: Partial<ViewOrganization> = {}): ViewOrganization {
    return {
        has_premium_features: true,
        id: 'organization-id',
        is_over_monthly_limit: false,
        is_over_request_limit: false,
        is_suspended: false,
        is_throttled: false,
        max_events_per_month: 3000,
        name: 'Acme',
        plan_id: 'EX_TRIAL',
        retention_days: 3,
        subscribe_date: null,
        trial_ends_utc: new Date(Date.now() + 10 * DAY).toISOString(),
        usage: [],
        ...overrides
    } as ViewOrganization;
}

function createUsage(total: number, limit = 3000): ViewOrganization['usage'] {
    const now = new Date();
    return [
        {
            blocked: 0,
            date: new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1)).toISOString(),
            deleted: 0,
            discarded: 0,
            limit,
            too_big: 0,
            total
        }
    ];
}

function renderNotifications() {
    return render(OrganizationNotifications, {
        isChatEnabled: false,
        openChat: vi.fn()
    });
}

describe('OrganizationNotifications', () => {
    beforeEach(() => {
        organizationRefetch.mockReset();
        projectsRefetch.mockReset();
        recordProductTour.mockReset();
        invalidateQueries.mockReset();
        projects[0]!.is_configured = false;
        state.me.organization_ids = ['organization-id'];
        state.me.product_tours = {};
        state.me.roles = [];
        state.organization = undefined;
        localStorage.clear();
        // Dismissing for a long-past day prunes every stored dismissal.
        dismissOrganizationNotification('trial-ending', 'reset', new Date(2000, 0, 1));
    });

    it('records configuration events before project data loads', () => {
        const configuredProjectIds = new Set<string>();

        const recorded = recordConfiguredProjectId(
            {
                change_type: ChangeType.Added,
                data: {},
                organization_id: 'organization-id',
                project_id: 'project-id',
                type: 'PersistentEvent'
            },
            'organization-id',
            configuredProjectIds
        );

        expect(recorded).toBe(true);
        expect(configuredProjectIds).toContain('project-id');
    });

    it('does not refetch organization or project state for persistent event changes', async () => {
        render(OrganizationNotifications, {
            isChatEnabled: false,
            openChat: vi.fn()
        });

        const message = {
            change_type: ChangeType.Added,
            organization_id: 'organization-id',
            project_id: 'project-id'
        };

        document.dispatchEvent(new CustomEvent('PersistentEventChanged', { detail: message }));
        document.dispatchEvent(new CustomEvent('PersistentEventChanged', { detail: message }));
        await tick();

        expect(organizationRefetch).not.toHaveBeenCalled();
        expect(projectsRefetch).not.toHaveBeenCalled();
    });

    it('does not refresh configuration state for an already configured project', async () => {
        projects[0]!.is_configured = true;
        render(OrganizationNotifications, {
            isChatEnabled: false,
            openChat: vi.fn()
        });

        document.dispatchEvent(
            new CustomEvent('PersistentEventChanged', {
                detail: {
                    change_type: ChangeType.Added,
                    organization_id: 'organization-id',
                    project_id: 'project-id'
                }
            })
        );
        await tick();

        expect(organizationRefetch).not.toHaveBeenCalled();
        expect(projectsRefetch).not.toHaveBeenCalled();
    });

    describe('trial and usage notices', () => {
        beforeEach(() => {
            projects[0]!.is_configured = true;
        });

        it('does not show a trial banner before the last three days', () => {
            // Arrange
            state.organization = createOrganization();

            // Act
            renderNotifications();

            // Assert
            expect(screen.queryByText(/free trial/)).toBeNull();
        });

        it('shows a dismissible trial banner in the last three days until the next day', async () => {
            // Arrange
            state.organization = createOrganization({ trial_ends_utc: new Date(Date.now() + 2 * DAY).toISOString() });

            // Act
            const { unmount } = renderNotifications();

            // Assert
            expect(screen.getByText(/The Acme free trial ends/).textContent).toContain('in 2 days');

            // Act
            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));

            // Assert
            expect(screen.queryByText(/free trial/)).toBeNull();

            // Act
            unmount();
            renderNotifications();

            // Assert
            expect(screen.queryByText(/free trial/)).toBeNull();
        });

        it('does not show a banner for a Free organization that never had a trial', () => {
            // Arrange
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: null });

            // Act
            renderNotifications();

            // Assert
            expect(screen.queryByRole('alert')).toBeNull();
        });

        it('restores the daily trial notice in an open tab without refetching organization state', async () => {
            // Arrange
            vi.useFakeTimers();
            vi.setSystemTime(new Date(2026, 10, 8, 23, 59, 30));
            state.organization = createOrganization({ trial_ends_utc: new Date(Date.now() + 2 * DAY).toISOString() });
            const { unmount } = renderNotifications();

            try {
                // Act
                await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
                const wasDismissed = screen.queryByText(/free trial/) === null;
                await vi.advanceTimersByTimeAsync(60_000);
                await tick();

                // Assert
                expect(wasDismissed).toBe(true);
                expect(screen.getByText(/The Acme free trial ends/).textContent).toContain('in 2 days');
                expect(organizationRefetch).not.toHaveBeenCalled();
                expect(projectsRefetch).not.toHaveBeenCalled();
            } finally {
                unmount();
                await tick();
                vi.useRealTimers();
            }
        });

        it('shows the trial-ended notice with the organization limits and records the dismissal', async () => {
            // Arrange
            state.organization = createOrganization({
                has_premium_features: false,
                plan_id: 'EX_FREE',
                trial_ends_utc: new Date(Date.now() - DAY).toISOString()
            });

            // Act
            renderNotifications();

            // Assert
            expect(screen.getByText('Your trial has ended. Acme is now on the Free plan.')).toBeTruthy();
            expect(screen.getByText(/3,000 events per month and 3 days of data retention/)).toBeTruthy();

            // Act
            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));

            // Assert
            expect(recordProductTour).toHaveBeenCalledWith({ tourName: 'trial-ended-organization-id', userId: 'user-id' }, expect.anything());
            expect(screen.queryByText(/Your trial has ended/)).toBeNull();
        });

        it('reloads the current user when the trial-ended dismissal fails to save', async () => {
            // Arrange
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: new Date(Date.now() - DAY).toISOString() });
            renderNotifications();

            // Act
            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
            recordProductTour.mock.calls[0]![1].onError();

            // Assert
            expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['User', 'me'] });
        });

        it('does not show the trial-ended notice to a global admin impersonating the organization', () => {
            // Arrange
            state.me.organization_ids = ['admin-organization-id'];
            state.me.roles = ['global'];
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: new Date(Date.now() - DAY).toISOString() });

            // Act
            renderNotifications();

            // Assert
            expect(screen.queryByText(/Your trial has ended/)).toBeNull();
        });

        it('does not show the trial-ended notice once the user dismissed it on any device', () => {
            // Arrange
            state.me.product_tours = { trial_ended_organization_id: '2026-10-20T00:00:00Z' };
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: new Date(Date.now() - DAY).toISOString() });

            // Act
            renderNotifications();

            // Assert
            expect(screen.queryByText(/Your trial has ended/)).toBeNull();
        });

        it('shows a dismissible usage warning at 80% of the monthly limit', async () => {
            // Arrange
            state.organization = createOrganization({ plan_id: 'EX_SMALL', trial_ends_utc: null, usage: createUsage(2550) });

            // Act
            const { unmount } = renderNotifications();

            // Assert
            expect(screen.getByText('Acme has used 85% of its monthly events.')).toBeTruthy();

            // Act
            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));

            // Assert
            expect(screen.queryByText(/monthly events/)).toBeNull();

            // Act
            unmount();
            renderNotifications();

            // Assert
            expect(screen.queryByText(/monthly events/)).toBeNull();
        });

        it('shows the usage warning ahead of the trial banner', () => {
            // Arrange
            state.organization = createOrganization({ trial_ends_utc: new Date(Date.now() + DAY).toISOString(), usage: createUsage(2700) });

            // Act
            renderNotifications();

            // Assert
            expect(screen.getByText('Acme has used 90% of its monthly events.')).toBeTruthy();
            expect(screen.queryByText(/free trial/)).toBeNull();
        });

        it('shows the monthly overage banner instead of the usage warning at the limit', () => {
            // Arrange
            state.organization = createOrganization({ is_over_monthly_limit: true, plan_id: 'EX_SMALL', trial_ends_utc: null, usage: createUsage(3000) });

            // Act
            renderNotifications();

            // Assert
            expect(screen.getByText('Acme has reached its monthly plan limit.')).toBeTruthy();
            expect(screen.queryByText(/of its monthly events/)).toBeNull();
            expect(screen.queryByRole('button', { name: 'Dismiss notification' })).toBeNull();
        });
    });
});
