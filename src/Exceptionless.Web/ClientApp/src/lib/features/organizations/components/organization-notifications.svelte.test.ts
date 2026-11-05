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

vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    useQueryClient: () => ({ invalidateQueries })
}));

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
            state.organization = createOrganization();

            renderNotifications();

            expect(screen.queryByText(/free trial/)).toBeNull();
        });

        it('shows a dismissible trial banner in the last three days until the next day', async () => {
            state.organization = createOrganization({ trial_ends_utc: new Date(Date.now() + 2 * DAY).toISOString() });

            const { unmount } = renderNotifications();
            expect(screen.getByText('2 days left in the Acme free trial.')).toBeTruthy();

            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
            expect(screen.queryByText(/free trial/)).toBeNull();

            unmount();
            renderNotifications();
            expect(screen.queryByText(/free trial/)).toBeNull();
        });

        it('does not show a banner for a Free organization that never had a trial', () => {
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: null });

            renderNotifications();

            expect(screen.queryByRole('alert')).toBeNull();
        });

        it('shows the trial-ended notice with the organization limits and records the dismissal', async () => {
            state.organization = createOrganization({
                has_premium_features: false,
                plan_id: 'EX_FREE',
                trial_ends_utc: new Date(Date.now() - DAY).toISOString()
            });

            renderNotifications();
            expect(screen.getByText('Your trial has ended. Acme is now on the Free plan.')).toBeTruthy();
            expect(screen.getByText(/3,000 events per month and 3 days of data retention/)).toBeTruthy();

            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));

            expect(recordProductTour).toHaveBeenCalledWith({ tourName: 'trial-ended-organization-id', userId: 'user-id' }, expect.anything());
            expect(screen.queryByText(/Your trial has ended/)).toBeNull();
        });

        it('reloads the current user when the trial-ended dismissal fails to save', async () => {
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: new Date(Date.now() - DAY).toISOString() });

            renderNotifications();
            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
            recordProductTour.mock.calls[0]![1].onError();

            expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['User', 'me'] });
        });

        it('does not show the trial-ended notice to a global admin impersonating the organization', () => {
            state.me.organization_ids = ['admin-organization-id'];
            state.me.roles = ['global'];
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: new Date(Date.now() - DAY).toISOString() });

            renderNotifications();

            expect(screen.queryByText(/Your trial has ended/)).toBeNull();
        });

        it('does not show the trial-ended notice once the user dismissed it on any device', () => {
            state.me.product_tours = { trial_ended_organization_id: '2026-10-20T00:00:00Z' };
            state.organization = createOrganization({ plan_id: 'EX_FREE', trial_ends_utc: new Date(Date.now() - DAY).toISOString() });

            renderNotifications();

            expect(screen.queryByText(/Your trial has ended/)).toBeNull();
        });

        it('shows a dismissible usage warning at 80% of the monthly limit', async () => {
            state.organization = createOrganization({ plan_id: 'EX_SMALL', trial_ends_utc: null, usage: createUsage(2550) });

            const { unmount } = renderNotifications();
            expect(screen.getByText('Acme has used 85% of its monthly events.')).toBeTruthy();

            await fireEvent.click(screen.getByRole('button', { name: 'Dismiss notification' }));
            expect(screen.queryByText(/monthly events/)).toBeNull();

            unmount();
            renderNotifications();
            expect(screen.queryByText(/monthly events/)).toBeNull();
        });

        it('shows the usage warning ahead of the trial banner', () => {
            state.organization = createOrganization({ trial_ends_utc: new Date(Date.now() + DAY).toISOString(), usage: createUsage(2700) });

            renderNotifications();

            expect(screen.getByText('Acme has used 90% of its monthly events.')).toBeTruthy();
            expect(screen.queryByText(/free trial/)).toBeNull();
        });

        it('shows the monthly overage banner instead of the usage warning at the limit', () => {
            state.organization = createOrganization({ is_over_monthly_limit: true, plan_id: 'EX_SMALL', trial_ends_utc: null, usage: createUsage(3000) });

            renderNotifications();

            expect(screen.getByText('Acme has reached its monthly plan limit.')).toBeTruthy();
            expect(screen.queryByText(/of its monthly events/)).toBeNull();
            expect(screen.queryByRole('button', { name: 'Dismiss notification' })).toBeNull();
        });
    });
});
