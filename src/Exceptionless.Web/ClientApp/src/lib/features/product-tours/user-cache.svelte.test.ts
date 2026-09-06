import type { ProductTourProgress, ViewCurrentUser } from '$generated/api';

import { putCurrentUserProductTour, queryKeys } from '$features/users/api.svelte';
import { MutationObserver, type MutationObserverOptions, QueryClient } from '@tanstack/svelte-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';

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

    it.each([
        { status: 1, version: 1 },
        { status: 2, version: 2 }
    ])('accepts forward progress: %o', async (progress) => {
        // Arrange
        queryClient.setQueryData(queryKeys.me(), {
            ...user('first-user'),
            product_tours: { 'app-overview': { status: 2, version: 1 } }
        });
        mocks.putJSON.mockResolvedValue({ data: progress, ok: true });

        // Act
        await putCurrentUserProductTour().mutateAsync({ progress, tourName: 'app-overview' });

        // Assert
        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.product_tours?.['app-overview']).toEqual(progress);
        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.id('first-user'))?.product_tours?.['app-overview']).toEqual(progress);
    });

    it.each([false, true])('applies delayed completion only to its original account (account changed: %s)', async (changeAccount) => {
        // Arrange
        const progress: ProductTourProgress = { status: 1, version: 1 };
        const request = Promise.withResolvers<{ data: ProductTourProgress; ok: boolean }>();
        mocks.putJSON.mockReturnValue(request.promise);
        const pending = putCurrentUserProductTour().mutateAsync({ progress, tourName: 'app-overview' });
        await vi.waitFor(() => expect(mocks.putJSON).toHaveBeenCalledOnce());
        const currentUser = { ...user(changeAccount ? 'second-user' : 'first-user'), full_name: 'Updated name' };
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
    it.each([
        { status: 1, version: 1 },
        { status: 2, version: 2 }
    ])('preserves newer cached progress when an older response arrives: %o', async (stored) => {
        // Arrange
        const progress: ProductTourProgress = { status: 2, version: 1 };
        const request = Promise.withResolvers<{ data: ProductTourProgress; ok: boolean }>();
        mocks.putJSON.mockReturnValue(request.promise);
        const pending = putCurrentUserProductTour().mutateAsync({ progress, tourName: 'app-overview' });
        await vi.waitFor(() => expect(mocks.putJSON).toHaveBeenCalledOnce());
        queryClient.setQueryData(queryKeys.me(), { ...user('first-user'), product_tours: { 'app-overview': stored } });

        // Act
        request.resolve({ data: progress, ok: true });
        await pending;

        // Assert
        expect(queryClient.getQueryData<ViewCurrentUser>(queryKeys.me())?.product_tours?.['app-overview']).toEqual(stored);
    });
});

function user(id: string): ViewCurrentUser {
    return { id, product_tours: {} } as ViewCurrentUser;
}
