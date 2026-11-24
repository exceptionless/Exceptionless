import { render } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import RootPage from './+page.svelte';

const mocks = vi.hoisted(() => ({
    goto: vi.fn(),
    onboarding: { continueSetupHref: '/project/project-1/configure', isAwaitingFirstEvent: true, isSettled: true }
}));

vi.mock('$app/navigation', () => ({ goto: mocks.goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path.replace('/(app)', '') || '/' }));
vi.mock('#features/onboarding/onboarding-state.svelte.ts', () => ({ useOrganizationOnboarding: () => mocks.onboarding }));
vi.mock('#features/organizations/api.svelte.ts', () => ({
    getOrganizationQuery: () => ({ data: undefined, isPending: false }),
    getOrganizationsQuery: () => ({ data: { data: [{ id: 'organization-1', name: 'Acme' }] }, isPending: false, isSuccess: true })
}));
vi.mock('#features/organizations/context.svelte.ts', () => ({ organization: { current: 'organization-1' } }));
vi.mock('#features/saved-views/api.svelte.ts', () => ({
    getSavedViewsQuery: () => ({ data: [], isPending: false }),
    isSavedViewDeleted: () => false
}));
vi.mock('#features/users/api.svelte.ts', () => ({ getMeQuery: () => ({ data: { organization_preferences: [] }, isPending: false }) }));

describe('default landing', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        Object.assign(mocks.onboarding, { isAwaitingFirstEvent: true, isSettled: true });
    });

    it('sends a first-run user to client setup', async () => {
        // Act
        render(RootPage);
        await tick();

        // Assert
        expect(mocks.goto).toHaveBeenCalledExactlyOnceWith('/project/project-1/configure?redirect=true', { replace: true });
    });

    it('opens the default view once events have arrived', async () => {
        // Arrange
        mocks.onboarding.isAwaitingFirstEvent = false;

        // Act
        render(RootPage);
        await tick();

        // Assert
        expect(mocks.goto).toHaveBeenCalledTimes(1);
        expect(mocks.goto.mock.calls[0]![0]).not.toContain('/configure');
    });

    it('waits until it knows whether events have arrived', async () => {
        // Arrange
        mocks.onboarding.isSettled = false;

        // Act
        render(RootPage);
        await tick();

        // Assert
        expect(mocks.goto).not.toHaveBeenCalled();
    });
});
