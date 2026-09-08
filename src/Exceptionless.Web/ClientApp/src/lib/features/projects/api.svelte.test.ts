import { QueryClient, QueryObserver, type QueryObserverOptions } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { ViewProject } from './models';

import { getProjectQuery, queryKeys } from './api.svelte';

const mocks = vi.hoisted(() => ({
    createQuery: vi.fn<(options: () => QueryObserverOptions<ViewProject>) => void>(),
    getJSON: vi.fn()
}));
vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'test-token' } }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: () => ({ getJSON: mocks.getJSON })
}));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createQuery: mocks.createQuery
}));

describe('project read lifecycle', () => {
    let client: QueryClient;
    const project: ViewProject = {
        created_utc: '2026-09-05T00:00:00Z',
        delete_bot_data_enabled: false,
        event_count: 0,
        has_premium_features: false,
        has_slack_integration: false,
        id: 'project-id',
        name: 'Example',
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
});
