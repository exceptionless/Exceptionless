import { describe, expect, it } from 'vitest';

import type { AssistantAccess } from './models';

import { resolveAssistantAccessState } from './access-state';

const available: AssistantAccess = { enabled: true, has_access: true, upgrade_required: false };

describe('resolveAssistantAccessState', () => {
    it.each([
        [undefined, undefined, false, false, false, 'disabled'],
        ['organization', undefined, true, false, true, 'loading'],
        ['organization', undefined, false, true, false, 'error'],
        ['organization', undefined, false, true, true, 'loading'],
        ['organization', available, false, false, true, 'available'],
        ['organization', { enabled: true, has_access: false, upgrade_required: true }, false, false, false, 'upgrade-required'],
        ['organization', { enabled: false, has_access: false, upgrade_required: false }, false, false, false, 'disabled']
    ] as const)('resolves an access-query state', (organizationId, access, isPending, isError, isFetching, expected) => {
        expect(resolveAssistantAccessState(organizationId, access, isPending, isError, isFetching)).toBe(expected);
    });
});
