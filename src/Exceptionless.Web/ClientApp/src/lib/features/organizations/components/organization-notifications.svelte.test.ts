import { render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ChangeType } from '#features/websockets/models.ts';

import OrganizationNotifications, { recordConfiguredProjectId } from './organization-notifications.svelte';

const organizationRefetch = vi.hoisted(() => vi.fn());
const projectsRefetch = vi.hoisted(() => vi.fn());
const projects = vi.hoisted(() => [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }]);
const organizationState = vi.hoisted(() => ({
    current: undefined as undefined | { has_premium_features: boolean; id: string; name: string; plan_name: string }
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
