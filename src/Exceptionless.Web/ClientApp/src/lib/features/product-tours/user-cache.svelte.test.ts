import type { ViewCurrentUser } from '$features/users/models';

import { putCurrentUserProductTour, queryKeys } from '$features/users/api.svelte';
import { MutationObserver, type MutationObserverOptions, QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ fetchApiJson: vi.fn(), useQueryClient: vi.fn() }));
vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('$features/shared/api/api.svelte', () => ({ fetchApiJson: mocks.fetchApiJson }));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createMutation: <TData, TError, TVariables, TContext>(options: () => MutationObserverOptions<TData, TError, TVariables, TContext>) => {
        const observer = new MutationObserver(mocks.useQueryClient(), options());
        return { mutateAsync: (variables: TVariables) => observer.mutate(variables) };
    },
    useQueryClient: mocks.useQueryClient
}));

describe('guided-tour user cache invalidation', () => {
    it.each([false, true])('invalidates user queries without overwriting account data (account changed: %s)', async (changeAccount) => {
        // Arrange
        vi.resetAllMocks();
        const queryClient = new QueryClient();
        mocks.useQueryClient.mockReturnValue(queryClient);
        const initial = { id: 'first-user', product_tours: {} } as ViewCurrentUser;
        const current = { ...initial, id: changeAccount ? 'second-user' : initial.id };
        queryClient.setQueryData(queryKeys.me(), initial);
        queryClient.setQueryData(queryKeys.id(initial.id), initial);
        const request = Promise.withResolvers<{ recorded_utc: string }>();
        mocks.fetchApiJson.mockReturnValue(request.promise);

        try {
            const pending = putCurrentUserProductTour().mutateAsync({ tourName: 'app-overview', userId: initial.id });
            await vi.waitFor(() => expect(mocks.fetchApiJson).toHaveBeenCalledOnce());
            expect(mocks.fetchApiJson).toHaveBeenCalledWith('users/me/product-tours/app-overview/record', { method: 'PUT' });
            queryClient.setQueryData(queryKeys.me(), current);

            // Act
            request.resolve({ recorded_utc: '2026-09-08T00:00:00Z' });
            await pending;

            // Assert
            expect(queryClient.getQueryData(queryKeys.me())).toEqual(current);
            expect(queryClient.getQueryState(queryKeys.me())?.isInvalidated).toBe(true);
            expect(queryClient.getQueryState(queryKeys.id(initial.id))?.isInvalidated).toBe(true);
        } finally {
            queryClient.clear();
        }
    });

    it('does not issue a request after the account changes before mutation execution', async () => {
        const queryClient = new QueryClient();
        mocks.useQueryClient.mockReturnValue(queryClient);
        queryClient.setQueryData(queryKeys.me(), { id: 'new-user', product_tours: {} } as ViewCurrentUser);

        await expect(putCurrentUserProductTour().mutateAsync({ tourName: 'app-overview', userId: 'old-user' })).rejects.toThrow('current user changed');
        expect(mocks.fetchApiJson).not.toHaveBeenCalled();
        queryClient.clear();
    });
});
