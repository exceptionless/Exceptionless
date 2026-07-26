import type { NewToken } from '$features/tokens/models';

export type ProjectTokenScope = 'client' | 'source-maps:write';

export function createProjectToken(organizationId: string, projectId: string, scope: ProjectTokenScope): NewToken {
    return {
        notes: scope === 'source-maps:write' ? 'Source map deployment' : undefined,
        organization_id: organizationId,
        project_id: projectId,
        scopes: [scope]
    };
}
