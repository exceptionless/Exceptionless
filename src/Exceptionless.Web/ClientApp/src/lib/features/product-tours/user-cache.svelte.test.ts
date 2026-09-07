import type { ProductTourProgress, ViewCurrentUser } from '$generated/api';

import { putCurrentUserProductTour, queryKeys } from '$features/users/api.svelte';
import { MutationObserver, type MutationObserverOptions, QueryClient, QueryObserver } from '@tanstack/svelte-query';
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
    it.each([false, true])('refetches authoritative progress without merging the response (account changed: %s)', async (changeAccount) => {
        // Arrange
        vi.resetAllMocks();
        const queryClient = new QueryClient();
        mocks.useQueryClient.mockReturnValue(queryClient);
        const initial = { id: 'first-user', product_tours: {} } as ViewCurrentUser;
        const current = { ...initial, id: changeAccount ? 'second-user' : initial.id };
        const serverUser = { ...current, product_tours: { 'app-overview': { status: 1, version: 1 } } } as ViewCurrentUser;
        queryClient.setQueryData(queryKeys.me(), initial);
        queryClient.setQueryData(queryKeys.id(initial.id), initial);
        const refresh = Promise.withResolvers<ViewCurrentUser>();
        const queryFn = vi.fn(() => refresh.promise);
        const observer = new QueryObserver(queryClient, { queryFn, queryKey: queryKeys.me(), staleTime: Infinity });
        const unsubscribe = observer.subscribe(() => {});
        const request = Promise.withResolvers<{ data: ProductTourProgress; ok: boolean }>();
        mocks.putJSON.mockReturnValue(request.promise);
        const progress: ProductTourProgress = { status: 2, version: 1 };

        try {
            const pending = putCurrentUserProductTour().mutateAsync({ progress, tourName: 'app-overview' });
            await vi.waitFor(() => expect(mocks.putJSON).toHaveBeenCalledOnce());
            queryClient.setQueryData(queryKeys.me(), current);

            // Act
            request.resolve({ data: progress, ok: true });
            await vi.waitFor(() => expect(queryFn).toHaveBeenCalledOnce());
            expect(queryClient.getQueryData(queryKeys.me())).toEqual(current);
            refresh.resolve(serverUser);
            await pending;

            // Assert
            expect(queryClient.getQueryData(queryKeys.me())).toEqual(serverUser);
            expect(queryClient.getQueryState(queryKeys.id(initial.id))?.isInvalidated).toBe(true);
            expect(queryClient.getQueryData(queryKeys.id(initial.id))).toEqual(initial);
        } finally {
            unsubscribe();
            queryClient.clear();
        }
    });
});
