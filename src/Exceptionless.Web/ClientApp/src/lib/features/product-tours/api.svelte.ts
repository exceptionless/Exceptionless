import type { PostProductTourActivity, ViewCurrentUser } from '$generated/api';

import { queryKeys as userQueryKeys } from '$features/users/api.svelte';
import { ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, useQueryClient } from '@tanstack/svelte-query';

import type { ProductTourKey, ProductTourLaunchSource } from './models';

export function createProductTourActivity() {
    const queryClient = useQueryClient();
    const client = useFetchClient();
    const activity = createMutation(() => ({
        mutationFn: async ({
            name,
            ...activity
        }: {
            action: `${NonNullable<PostProductTourActivity['action']>}`;
            name: ProductTourKey;
            source: ProductTourLaunchSource;
            step?: string;
            version: number;
        }) => {
            if (queryClient.getQueryData<ViewCurrentUser>(userQueryKeys.me())?.product_tour_analytics_enabled !== true) {
                return;
            }
            await client.postJSON(`users/me/product-tours/${name}/activity`, activity);
        },
        mutationKey: ['product-tours', 'activity'],
        retry: false
    }));

    return async (
        action: `${NonNullable<PostProductTourActivity['action']>}`,
        name: ProductTourKey,
        version: number,
        source: ProductTourLaunchSource,
        step?: string
    ): Promise<void> => {
        try {
            await activity.mutateAsync({
                action,
                name,
                source,
                step,
                version
            });
        } catch {
            // Optional analytics must not interrupt the guide or expose request context through the telemetry SDK.
        }
    };
}

export function putProductTourAnalytics() {
    const queryClient = useQueryClient();
    const client = useFetchClient();
    return createMutation<void, ProblemDetails, boolean, { previous: undefined | ViewCurrentUser }>(() => ({
        mutationFn: async (enabled) => {
            await client.putJSON('users/me/product-tour-analytics', {
                enabled
            });
        },
        mutationKey: ['product-tours', 'analytics'],
        onError: (_error, _enabled, context) => {
            const previous = context?.previous;
            if (previous) {
                queryClient.setQueryData<ViewCurrentUser>(userQueryKeys.me(), (user) => {
                    if (!user || user.id !== previous.id) {
                        return user;
                    }
                    return {
                        ...user,
                        product_tour_analytics_enabled: previous.product_tour_analytics_enabled
                    };
                });
            }
        },
        onMutate: async (enabled) => {
            await queryClient.cancelQueries({
                queryKey: userQueryKeys.me()
            });
            const previous = queryClient.getQueryData<ViewCurrentUser>(userQueryKeys.me());
            queryClient.setQueryData<ViewCurrentUser>(
                userQueryKeys.me(),
                (user) =>
                    user && {
                        ...user,
                        product_tour_analytics_enabled: enabled
                    }
            );
            return {
                previous
            };
        }
    }));
}
