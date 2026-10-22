import { render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ChangeType } from '#features/websockets/models.ts';

import OrganizationNotifications, {
    FREE_PLAN_NOTIFICATION_GRACE_DAYS,
    isWithinFreePlanGracePeriod,
    recordConfiguredProjectId
} from './organization-notifications.svelte';

const organizationRefetch = vi.hoisted(() => vi.fn());
const projectsRefetch = vi.hoisted(() => vi.fn());
const projects = vi.hoisted(() => [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }]);
const organizationState = vi.hoisted(() => ({
    current: undefined as
        | undefined
        | {
              created_utc?: string;
              has_premium_features: boolean;
              id: string;
              is_over_monthly_limit?: boolean;
              is_suspended?: boolean;
              name: string;
              plan_name: string;
          }
}));

vi.mock('$app/paths', () => ({
    resolve: (path: string, params?: Record<string, string>) =>
        Object.entries(params ?? {}).reduce((href, [key, value]) => href.replace(`[${key}]`, value), path.replace('/(app)', ''))
}));

vi.mock('#features/organizations/api.svelte.ts', () => ({
    getOrganizationQuery: () => ({ data: organizationState.current, refetch: organizationRefetch }),
    getOrganizationsQuery: () => ({ data: { data: [] } })
}));

vi.mock('#features/organizations/context.svelte.ts', () => ({
    organization: { current: 'organization-id' }
}));

vi.mock('#features/projects/api.svelte.ts', () => ({
    getOrganizationProjectsQuery: () => ({ data: { data: projects }, isSuccess: true, refetch: projectsRefetch })
}));

vi.mock('#features/users/api.svelte.ts', () => ({
    getMeQuery: () => ({ data: { organization_ids: ['organization-id'], roles: [] } })
}));

describe('OrganizationNotifications', () => {
    beforeEach(() => {
        organizationRefetch.mockReset();
        projectsRefetch.mockReset();
        organizationState.current = undefined;
        projects.splice(0, projects.length, { id: 'project-id', is_configured: false, organization_id: 'organization-id' });
    });

    it.each([
        ['no projects', [], {}, 'Set up your first project'],
        [
            'only unconfigured projects',
            [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }],
            {},
            "We haven't received any events yet!"
        ],
        ['no projects on a premium page', [], { premiumFeatureName: 'Sessions', requiresPremium: true }, 'Set up your first project'],
        [
            'a configured project',
            [{ id: 'project-id', is_configured: true, organization_id: 'organization-id' }],
            {},
            'Free Organization is currently on a free plan.'
        ]
    ])('shows setup prompts before free plan upsells for an organization with %s', (_, organizationProjects, props, title) => {
        // Arrange
        organizationState.current = { has_premium_features: false, id: 'organization-id', name: 'Free Organization', plan_name: 'Free' };
        projects.splice(0, projects.length, ...organizationProjects);

        // Act
        render(OrganizationNotifications, {
            isChatEnabled: false,
            openChat: vi.fn(),
            ...props
        });

        // Assert
        expect(screen.getByText(title)).toBeInTheDocument();
        expect(screen.getAllByRole('alert')).toHaveLength(1);
    });

    describe('free plan upsell', () => {
        const configuredProjects = [{ id: 'project-id', is_configured: true, organization_id: 'organization-id' }];
        const daysAgo = (days: number) => new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString();

        function renderFor(organization: Partial<NonNullable<typeof organizationState.current>>) {
            organizationState.current = {
                has_premium_features: false,
                id: 'organization-id',
                name: 'Free Organization',
                plan_name: 'Free',
                ...organization
            };
            projects.splice(0, projects.length, ...configuredProjects);
            render(OrganizationNotifications, { isChatEnabled: false, openChat: vi.fn() });
        }

        it('hides the upsell for an organization created within the grace period', () => {
            // Arrange & Act
            renderFor({ created_utc: daysAgo(FREE_PLAN_NOTIFICATION_GRACE_DAYS - 1) });

            // Assert
            expect(screen.queryAllByRole('alert')).toHaveLength(0);
        });

        it('shows the upsell once the grace period has passed', () => {
            // Arrange & Act
            renderFor({ created_utc: daysAgo(FREE_PLAN_NOTIFICATION_GRACE_DAYS + 1) });

            // Assert
            expect(screen.getByText('Free Organization is currently on a free plan.')).toBeInTheDocument();
        });

        it.each([
            ['suspension', { is_suspended: true }, 'Free Organization has been suspended.'],
            ['monthly overage', { is_over_monthly_limit: true }, 'Free Organization has reached its monthly plan limit.']
        ])('still shows %s for a new organization', (_, state, title) => {
            // Arrange & Act
            renderFor({ created_utc: daysAgo(0), ...state });

            // Assert
            expect(screen.getByText(title)).toBeInTheDocument();
        });

        it.each([
            [undefined, false],
            ['not a date', false],
            [new Date(Date.now() - 60_000).toISOString(), true]
        ])('treats created_utc %s as within the grace period: %s', (createdUtc, expected) => {
            // Arrange & Act
            const actual = isWithinFreePlanGracePeriod(createdUtc);

            // Assert
            expect(actual).toBe(expected);
        });
    });

    it.each([
        ['no projects', []],
        ['only unconfigured projects', [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }]]
    ])('leaves the setup prompt to pages with their own for an organization with %s', (_, organizationProjects) => {
        // Arrange
        organizationState.current = { has_premium_features: false, id: 'organization-id', name: 'Free Organization', plan_name: 'Free' };
        projects.splice(0, projects.length, ...organizationProjects);

        // Act
        render(OrganizationNotifications, {
            ignoreSetupPrompts: true,
            isChatEnabled: false,
            openChat: vi.fn()
        });

        // Assert
        expect(screen.queryAllByRole('alert')).toHaveLength(0);
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
});
