import { flushSync } from 'svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { useOrganizationOnboarding } from './onboarding-state.svelte';

const mocks = vi.hoisted(() => ({
    count: { data: undefined as undefined | { total: number }, isError: false, isSuccess: true },
    projects: { data: undefined as undefined | { data: object[] }, isSuccess: true }
}));

vi.mock('#features/events/api.svelte.ts', () => ({ getOrganizationCountQuery: () => mocks.count }));
vi.mock('#features/projects/api.svelte.ts', () => ({ getOrganizationProjectsQuery: () => mocks.projects }));

describe('useOrganizationOnboarding', () => {
    let cleanup: (() => void) | undefined;

    afterEach(() => cleanup?.());

    function setup(projects: object[], total: number) {
        mocks.projects.data = { data: projects };
        mocks.count.data = { total };
        let onboarding!: ReturnType<typeof useOrganizationOnboarding>;
        cleanup = $effect.root(() => {
            onboarding = useOrganizationOnboarding(() => 'organization-id');
        });
        flushSync();
        return onboarding;
    }

    it.each([
        ['an organization event count but no projects', [], 5, false],
        ['an organization event count and a project', [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }], 5, true],
        ['a project and no events', [{ id: 'project-id', is_configured: false, organization_id: 'organization-id' }], 0, false]
    ])('reports events for %s', (_, projects, total, expected) => {
        // Arrange & Act
        const onboarding = setup(projects, total);

        // Assert
        expect(onboarding.hasReceivedEvents).toBe(expected);
    });
});
