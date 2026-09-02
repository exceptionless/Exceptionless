import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { UpdateSavedViewOrder } from './models';

const fetchClientMocks = vi.hoisted(() => ({ putJSON: vi.fn() }));
const queryClient = vi.hoisted(() => ({}));
const setCurrentUserSavedViewOrder = vi.hoisted(() => vi.fn());

vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'access-token' } }));
vi.mock('$features/organizations/api.svelte', () => ({ setOrganizationDefaultSavedView: vi.fn() }));
vi.mock('$features/users/api.svelte', () => ({
    setCurrentUserSavedViewDefault: vi.fn(),
    setCurrentUserSavedViewOrder
}));
vi.mock('@foundatiofx/fetchclient', () => ({
    useFetchClient: () => ({ putJSON: fetchClientMocks.putJSON })
}));
vi.mock('@tanstack/svelte-query', () => ({
    createMutation: (options: () => unknown) => options(),
    createQuery: vi.fn(),
    useQueryClient: () => queryClient
}));

import { putUserSavedViewOrder } from './api.svelte';

type SavedViewOrderMutationOptions = {
    mutationFn: (variables: UpdateSavedViewOrder & { view_type: string }) => Promise<{
        order: UpdateSavedViewOrder;
        organizationId: string | undefined;
    }>;
    onSuccess: (data: { order: UpdateSavedViewOrder; organizationId: string | undefined }, variables: UpdateSavedViewOrder & { view_type: string }) => void;
};

describe('putUserSavedViewOrder', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('updates the cache for the organization that originated the request', async () => {
        const route = { organizationId: 'organization-a' as string | undefined };
        const variables = { saved_view_ids: ['saved-view-id'], view_type: 'events' };
        fetchClientMocks.putJSON.mockResolvedValue({ data: { saved_view_ids: variables.saved_view_ids } });
        const mutation = putUserSavedViewOrder({ route }) as unknown as SavedViewOrderMutationOptions;

        const resultPromise = mutation.mutationFn(variables);
        route.organizationId = 'organization-b';
        const result = await resultPromise;
        mutation.onSuccess(result, variables);

        expect(fetchClientMocks.putJSON).toHaveBeenCalledWith('organizations/organization-a/saved-view-order/events', {
            saved_view_ids: variables.saved_view_ids
        });
        expect(setCurrentUserSavedViewOrder).toHaveBeenCalledWith(queryClient, 'organization-a', 'events', variables.saved_view_ids);
    });
});
