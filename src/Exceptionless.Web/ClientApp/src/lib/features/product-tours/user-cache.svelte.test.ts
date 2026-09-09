import type { ViewCurrentUser } from '$features/users/models';

import { putCurrentUserProductTour, queryKeys } from '$features/users/api.svelte';
import { MutationObserver, type MutationObserverOptions, QueryClient } from '@tanstack/svelte-query';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ putJSON: vi.fn(), useQueryClient: vi.fn() }));
vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: () => ({ putJSON: mocks.putJSON })
}));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createMutation: <TData, TError, TVariables, TContext>(options: () => MutationObserverOptions<TData, TError, TVariables, TContext>) => {
        const observer = new MutationObserver(mocks.useQueryClient(), options());
        return { mutateAsync: (variables: TVariables) => observer.mutate(variables) };
    },
    useQueryClient: mocks.useQueryClient
}));

describe('guided-tour user cache invalidation', () => {
    it.each([false, true])('only applies a record to the captured account (account changed: %s)', async (changeAccount) => {
        // Arrange
        vi.resetAllMocks();
        const queryClient = new QueryClient();
        mocks.useQueryClient.mockReturnValue(queryClient);
        const initial = { id: 'first-user', product_tours: {} } as ViewCurrentUser;
        const current = { ...initial, id: changeAccount ? 'second-user' : initial.id };
        queryClient.setQueryData(queryKeys.me(), initial);
        queryClient.setQueryData(queryKeys.id(initial.id), initial);
        const request = Promise.withResolvers<{ data: { recorded_utc: string }; ok: boolean }>();
        mocks.putJSON.mockReturnValue(request.promise);

        try {
            const pending = putCurrentUserProductTour().mutateAsync({ recordName: 'app-overview', stateKey: 'app_overview', userId: initial.id });
            await vi.waitFor(() => expect(mocks.putJSON).toHaveBeenCalledOnce());
            expect(mocks.putJSON).toHaveBeenCalledWith('users/me/product-tours/app-overview/record');
            queryClient.setQueryData(queryKeys.me(), current);

            // Act
            request.resolve({ data: { recorded_utc: '2026-09-08T00:00:00Z' }, ok: true });
            await pending;

            // Assert
            expect(queryClient.getQueryData(queryKeys.me())).toEqual(
                changeAccount ? current : { ...initial, product_tours: { app_overview: '2026-09-08T00:00:00Z' } }
            );
        } finally {
            queryClient.clear();
        }
    });

    it('does not issue a request after the account changes before mutation execution', async () => {
        const queryClient = new QueryClient();
        mocks.useQueryClient.mockReturnValue(queryClient);
        queryClient.setQueryData(queryKeys.me(), { id: 'new-user', product_tours: {} } as ViewCurrentUser);

        await expect(putCurrentUserProductTour().mutateAsync({ recordName: 'app-overview', stateKey: 'app_overview', userId: 'old-user' })).rejects.toThrow(
            'current user changed'
        );
        expect(mocks.putJSON).not.toHaveBeenCalled();
        queryClient.clear();
    });
});
