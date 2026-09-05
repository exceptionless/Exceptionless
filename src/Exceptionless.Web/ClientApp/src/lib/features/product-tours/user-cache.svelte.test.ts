import type { ProductTourProgress, ViewCurrentUser } from '$generated/api';

import { putCurrentUserProductTour, queryKeys } from '$features/users/api.svelte';
import { MutationObserver, type MutationObserverOptions, QueryClient } from '@tanstack/svelte-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { putProductTourAnalytics } from './api.svelte';

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

describe('guided-tour user cache concurrency', () => {
    let queryClient: QueryClient;

    beforeEach(() => {
        vi.resetAllMocks();
        queryClient = new QueryClient();
        mocks.useQueryClient.mockReturnValue(queryClient);
        queryClient.setQueryData(queryKeys.me(), user('first-user'));
    });

    it.each([false, true])('keeps the acknowledged preference without refetching the user (enabled: %s)', async (enabled) => {
        // Arrange
        mocks.putJSON.mockResolvedValue(undefined);
        const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

        // Act
        await putProductTourAnalytics().mutateAsync(enabled);

        // Assert
        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.product_tour_analytics_enabled).toBe(enabled);
        expect(invalidate).not.toHaveBeenCalled();
        expect(queryClient.getQueryState(queryKeys.me())?.isInvalidated).toBe(false);
    });

    it('preserves progress saved while a preference update fails', async () => {
        // Arrange
        const request = Promise.withResolvers<void>();
        mocks.putJSON.mockReturnValue(request.promise);
        const pending = putProductTourAnalytics()
            .mutateAsync(false)
            .catch(() => undefined);
        await vi.waitFor(() => expect(mocks.putJSON).toHaveBeenCalledOnce());
        queryClient.setQueryData<ViewCurrentUser>(queryKeys.me(), (current) => ({
            ...current!,
            product_tours: { 'app-overview': { status: 1, version: 1 } }
        }));

        // Act
        request.reject(new Error('Unavailable'));
        await pending;

        // Assert
        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())).toMatchObject({
            product_tour_analytics_enabled: true,
            product_tours: { 'app-overview': { status: 1, version: 1 } }
        });
    });

    it('does not restore the previous account when its preference update fails', async () => {
        // Arrange
        const request = Promise.withResolvers<void>();
        mocks.putJSON.mockReturnValue(request.promise);
        const pending = putProductTourAnalytics()
            .mutateAsync(false)
            .catch(() => undefined);
        await vi.waitFor(() => expect(mocks.putJSON).toHaveBeenCalledOnce());
        const nextUser = user('second-user');
        queryClient.setQueryData(queryKeys.me(), nextUser);

        // Act
        request.reject(new Error('Unavailable'));
        await pending;

        // Assert
        expect(queryClient.getQueryData(queryKeys.me())).toEqual(nextUser);
    });

    it.each([false, true])('applies delayed completion only to its original account (account changed: %s)', async (changeAccount) => {
        // Arrange
        const progress: ProductTourProgress = { status: 1, version: 1 };
        const request = Promise.withResolvers<{ data: ProductTourProgress; ok: boolean }>();
        mocks.putJSON.mockReturnValue(request.promise);
        const pending = putCurrentUserProductTour().mutateAsync({ progress, tourName: 'app-overview' });
        await vi.waitFor(() => expect(mocks.putJSON).toHaveBeenCalledOnce());
        const currentUser = { ...user(changeAccount ? 'second-user' : 'first-user'), product_tour_analytics_enabled: false };
        queryClient.setQueryData(queryKeys.me(), currentUser);

        // Act
        request.resolve({ data: progress, ok: true });
        await pending;

        // Assert
        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())).toEqual({
            ...currentUser,
            product_tours: changeAccount ? {} : { 'app-overview': progress }
        });
    });
});

function user(id: string): ViewCurrentUser {
    return { id, product_tour_analytics_enabled: true, product_tours: {} } as ViewCurrentUser;
}
