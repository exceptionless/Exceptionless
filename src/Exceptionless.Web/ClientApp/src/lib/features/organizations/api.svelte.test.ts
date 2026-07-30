import { describe, expect, it, vi } from 'vitest';

const { mutationOptions, queryClient } = vi.hoisted(() => ({
    mutationOptions: [] as { onMutate?: (variables: { organizationId: string }) => Promise<unknown> }[],
    queryClient: { cancelQueries: vi.fn(async () => {}) }
}));

vi.mock('$features/auth/index.svelte', () => ({
    accessToken: { current: 'token' }
}));

vi.mock('$features/shared/api/api.svelte', () => ({
    fetchApiJson: vi.fn()
}));

vi.mock('$features/users/api.svelte', () => ({
    queryKeys: { me: () => ['User', 'me'] }
}));

vi.mock('@foundatiofx/fetchclient', () => ({
    useFetchClient: vi.fn()
}));

vi.mock('@tanstack/svelte-query', () => ({
    createMutation: (options: () => unknown) => {
        const mutation = options() as { onMutate?: (variables: { organizationId: string }) => Promise<unknown> };
        mutationOptions.push(mutation);
        return mutation;
    },
    createQuery: vi.fn(),
    useQueryClient: () => queryClient
}));

import { deleteOrganizationDataMutation, postOrganizationDataMutation, queryKeys } from './api.svelte';

describe('organization data mutations', () => {
    it('cancels an in-flight organization read before each data write', async () => {
        const organizationId = 'organization-id';

        postOrganizationDataMutation();
        deleteOrganizationDataMutation();

        const postMutation = mutationOptions[0]!;
        const deleteMutation = mutationOptions[1]!;
        await postMutation.onMutate?.({ organizationId });
        await deleteMutation.onMutate?.({ organizationId });

        expect(queryClient.cancelQueries).toHaveBeenNthCalledWith(1, { queryKey: queryKeys.id(organizationId, undefined) });
        expect(queryClient.cancelQueries).toHaveBeenNthCalledWith(2, { queryKey: queryKeys.id(organizationId, undefined) });
    });
});
