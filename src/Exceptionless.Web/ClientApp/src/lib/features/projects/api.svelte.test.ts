import { type MutationObserverOptions, QueryClient, QueryObserver, type QueryObserverOptions } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ViewProject } from './models';

import { deleteProject, getProjectQuery, getProjectsQuery, queryKeys } from './api.svelte';

const mocks = vi.hoisted(() => ({
    createMutation: vi.fn<(options: () => MutationObserverOptions<unknown, Error, void>) => void>(),
    createQuery: vi.fn<(options: () => QueryObserverOptions<ViewProject>) => void>(),
    getJSON: vi.fn(),
    queryClient: undefined as QueryClient | undefined
}));
vi.mock('#features/auth/index.svelte.ts', () => ({ accessToken: { current: 'test-token' } }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: () => ({ getJSON: mocks.getJSON })
}));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createMutation: mocks.createMutation,
    createQuery: mocks.createQuery,
    useQueryClient: () => mocks.queryClient
}));

describe('project read lifecycle', () => {
    let client: QueryClient;
    const project: ViewProject = {
        created_utc: '2026-09-05T00:00:00Z',
        delete_bot_data_enabled: false,
        discard_events_without_environment: false,
        discard_unlisted_environments: false,
        environments: [],
        event_count: 0,
        has_premium_features: false,
        has_slack_integration: false,
        id: 'project-id',
        name: 'Example',
        notify_other_environments: true,
        organization_id: 'organization-id',
        organization_name: 'Example organization',
        promoted_tabs: [],
        stack_count: 0,
        usage: [],
        usage_hours: []
    };

    beforeEach(() => {
        vi.resetAllMocks();
        client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
        mocks.queryClient = client;
    });

    afterEach(() => client.clear());

    it('reuses an in-flight read after its last observer unmounts', async () => {
        // Arrange
        const pending = Promise.withResolvers<{ data: ViewProject }>();
        mocks.getJSON.mockReturnValue(pending.promise);
        getProjectQuery({ route: { id: 'project-id' } });
        const options = mocks.createQuery.mock.calls[0]![0]();
        const first = new QueryObserver(client, options);
        const unsubscribe = first.subscribe(() => {});
        await vi.waitFor(() => expect(mocks.getJSON).toHaveBeenCalledOnce());

        // Act
        unsubscribe();
        const second = new QueryObserver(client, options);
        const stop = second.subscribe(() => {});
        pending.resolve({ data: project });

        // Assert
        await vi.waitFor(() => expect(second.getCurrentResult().data).toEqual(project));
        expect(mocks.getJSON).toHaveBeenCalledExactlyOnceWith('projects/project-id');
        stop();
    });

    it.each(['cancel', 'clear'])('does not cache a late response after explicit %s', async (operation) => {
        // Arrange
        const pending = Promise.withResolvers<{ data: ViewProject }>();
        mocks.getJSON.mockReturnValue(pending.promise);
        getProjectQuery({ route: { id: 'project-id' } });
        const observer = new QueryObserver(client, mocks.createQuery.mock.calls[0]![0]());
        const stop = observer.subscribe(() => {});
        await vi.waitFor(() => expect(mocks.getJSON).toHaveBeenCalledOnce());

        // Act
        if (operation === 'cancel') {
            await client.cancelQueries({ queryKey: queryKeys.id('project-id') });
        } else {
            client.clear();
        }
        pending.resolve({ data: project });
        await vi.waitFor(() => expect(mocks.getJSON.mock.settledResults[0]?.type).toBe('fulfilled'));

        // Assert
        expect(client.getQueryData(queryKeys.id('project-id'))).toBeUndefined();
        stop();
    });

    it('marks the all-projects list stale when the project query type is invalidated', async () => {
        // Arrange
        mocks.getJSON.mockResolvedValue({ data: [project] });
        getProjectsQuery({ params: { mode: 'stats' } });
        const observer = new QueryObserver(client, mocks.createQuery.mock.calls[0]![0]());
        const stop = observer.subscribe(() => {});
        await vi.waitFor(() => expect(observer.getCurrentResult().data?.data).toEqual([project]));
        const queryKey = observer.getCurrentQuery().queryKey;
        expect(client.getQueryState(queryKey)?.isInvalidated).toBe(false);

        // Act
        await client.invalidateQueries({ queryKey: queryKeys.type, refetchType: 'none' });

        // Assert
        expect(queryKey.slice(0, 2)).toEqual(queryKeys.projects());
        expect(client.getQueryState(queryKey)?.isInvalidated).toBe(true);
        stop();
    });

    it('removes deleted projects from cached organization lists and refreshes the project queries', async () => {
        // Arrange
        const otherProject = { ...project, id: 'other-project-id' };
        const listKey = (organizationId: string) => [...queryKeys.organization(organizationId), { params: { limit: 1000 } }];
        client.setQueryData(listKey('organization-id'), { data: [project, otherProject] });
        client.setQueryData(listKey('other-organization-id'), { data: [{ ...project, id: 'unrelated-project-id' }] });
        deleteProject({ route: { ids: ['project-id'] } });
        const options = mocks.createMutation.mock.calls[0]![0]();

        // Act
        await options.onSuccess!(undefined as never, undefined as never, undefined as never, undefined as never);

        // Assert
        expect(client.getQueryData<{ data: ViewProject[] }>(listKey('organization-id'))?.data.map((p) => p.id)).toEqual(['other-project-id']);
        expect(client.getQueryData<{ data: ViewProject[] }>(listKey('other-organization-id'))?.data.map((p) => p.id)).toEqual(['unrelated-project-id']);
        expect(client.getQueryState(listKey('organization-id'))?.isInvalidated).toBe(true);
    });
});
