import { describe, expect, it } from 'vitest';

import { createProjectToken } from './project-token';

describe('createProjectToken', () => {
    it('creates a project-scoped source map upload token', () => {
        const token = createProjectToken('organization-id', 'project-id', 'source-maps:write');

        expect(token).toEqual({
            notes: 'Source map deployment',
            organization_id: 'organization-id',
            project_id: 'project-id',
            scopes: ['source-maps:write']
        });
    });

    it('preserves normal client API key creation', () => {
        const token = createProjectToken('organization-id', 'project-id', 'client');

        expect(token).toEqual({
            notes: undefined,
            organization_id: 'organization-id',
            project_id: 'project-id',
            scopes: ['client']
        });
    });
});
