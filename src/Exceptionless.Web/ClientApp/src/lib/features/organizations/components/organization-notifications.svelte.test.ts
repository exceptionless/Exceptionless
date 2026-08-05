import { ChangeType } from '$features/websockets/models';
import { render } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import OrganizationNotifications from './organization-notifications.svelte';

const organizationRefetch = vi.hoisted(() => vi.fn());
const projectsRefetch = vi.hoisted(() => vi.fn());
const projects = vi.hoisted(() => [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }]);

vi.mock('$features/organizations/api.svelte', () => ({
    getOrganizationQuery: () => ({ data: undefined, refetch: organizationRefetch }),
    getOrganizationsQuery: () => ({ data: { data: [] } })
}));

vi.mock('$features/organizations/context.svelte', () => ({
    organization: { current: 'organization-id' }
}));

vi.mock('$features/projects/api.svelte', () => ({
    getOrganizationProjectsQuery: () => ({ data: { data: projects }, isSuccess: true, refetch: projectsRefetch })
}));

vi.mock('$features/users/api.svelte', () => ({
    getMeQuery: () => ({ data: { organization_ids: ['organization-id'], roles: [] } })
}));

describe('OrganizationNotifications', () => {
    beforeEach(() => {
        organizationRefetch.mockReset();
        projectsRefetch.mockReset();
        projects[0]!.is_configured = false;
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
