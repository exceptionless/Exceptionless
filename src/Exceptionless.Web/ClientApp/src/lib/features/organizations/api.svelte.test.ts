import type { QueryClient as QueryClientType } from '@tanstack/svelte-query';

import { afterEach, describe, expect, it, vi } from 'vitest';

const { mutationOptions } = vi.hoisted(() => ({
    mutationOptions: [] as {
        onMutate: (variables: { organizationId: string }) => Promise<unknown>;
        onSettled: () => Promise<void>;
        onSuccess: (result: boolean, variables: { key: string; organizationId: string; value: string }) => Promise<void>;
    }[]
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

vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createMutation: (options: () => unknown) => {
        const mutation = options() as (typeof mutationOptions)[number];
        mutationOptions.push(mutation);
        return mutation;
    },
    createQuery: vi.fn(),
    useQueryClient: () => queryClient
}));

import { QueryClient, QueryObserver } from '@tanstack/svelte-query';

import { deleteOrganizationDataMutation, postOrganizationDataMutation, queryKeys } from './api.svelte';

const queryClient: QueryClientType = new QueryClient();

afterEach(() => {
    queryClient.clear();
    mutationOptions.length = 0;
    vi.restoreAllMocks();
});

describe('organization data mutations', () => {
    it('cancels an in-flight organization read before each data write', async () => {
        const organizationId = 'organization-id';
        const cancelQueries = vi.spyOn(queryClient, 'cancelQueries');

        postOrganizationDataMutation();
        deleteOrganizationDataMutation();

        const postMutation = mutationOptions[0]!;
        const deleteMutation = mutationOptions[1]!;
        await postMutation.onMutate?.({ organizationId });
        await deleteMutation.onMutate?.({ organizationId });

        expect(cancelQueries).toHaveBeenCalledWith({ queryKey: queryKeys.id(organizationId, undefined) });
    });

    it.each(['before', 'during'])('prevents a list read started %s a write from replacing saved billing data', async (readTiming) => {
        const organizationId = 'organization-id';
        const organization = { data: { billing_name: 'Old name', unrelated_key: 'preserved' }, id: organizationId };
        const listKey = queryKeys.list(undefined);
        const oldResponse = { data: [organization] };
        queryClient.setQueryData(listKey, oldResponse);
        queryClient.setQueryData(queryKeys.id(organizationId, undefined), organization);
        const pendingRead = Promise.withResolvers<typeof oldResponse>();
        postOrganizationDataMutation();
        const mutation = mutationOptions[0]!;
        if (readTiming === 'during') {
            await mutation.onMutate({ organizationId });
        }
        const read = queryClient.fetchQuery({ queryFn: () => pendingRead.promise, queryKey: listKey }).catch(() => undefined);
        if (readTiming === 'before') {
            await mutation.onMutate({ organizationId });
        }
        await mutation.onSuccess(true, { key: 'billing_name', organizationId, value: 'New name' });
        pendingRead.resolve(oldResponse);
        await read;

        expect(queryClient.getQueryData(listKey)).toEqual({ data: [{ ...organization, data: { billing_name: 'New name', unrelated_key: 'preserved' } }] });
        expect(queryClient.getQueryData(queryKeys.id(organizationId, undefined))).toEqual({
            ...organization,
            data: { billing_name: 'New name', unrelated_key: 'preserved' }
        });
    });

    it.each([true, false])('restarts an initial list read after a billing mutation settles (success: %s)', async (success) => {
        const organizationId = 'organization-id';
        const response = { data: [{ data: { billing_name: 'Saved name' }, id: organizationId }] };
        const initialRead = Promise.withResolvers<typeof response>();
        const queryFn = vi.fn().mockReturnValueOnce(initialRead.promise).mockResolvedValue(response);
        const observer = new QueryObserver(queryClient, { queryFn, queryKey: queryKeys.list(undefined) });
        const unsubscribe = observer.subscribe(() => {});

        try {
            expect(queryFn).toHaveBeenCalledTimes(1);
            postOrganizationDataMutation();
            const mutation = mutationOptions[0]!;
            await mutation.onMutate({ organizationId });
            if (success) {
                await mutation.onSuccess(true, { key: 'billing_name', organizationId, value: 'Saved name' });
            }
            await mutation.onSettled();

            expect(queryFn).toHaveBeenCalledTimes(2);
            expect(observer.getCurrentResult().status).toBe('success');
            expect(observer.getCurrentResult().data).toEqual(response);
        } finally {
            unsubscribe();
            initialRead.resolve(response);
        }
    });
});
