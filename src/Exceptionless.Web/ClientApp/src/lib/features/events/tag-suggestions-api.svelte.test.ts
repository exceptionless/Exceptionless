import type { CountResult } from '$shared/models';

import { ChangeType } from '$features/websockets/models';
import { QueryClient, QueryObserver, type QueryObserverOptions } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { getTagSuggestionsQuery, invalidatePersistentEventQueries } from './api.svelte';

const mocks = vi.hoisted(() => ({
    accessToken: { current: 'session-a' as null | string },
    createQuery: vi.fn<(options: () => QueryObserverOptions<CountResult>) => void>(),
    getJSON: vi.fn()
}));
vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('$features/auth/index.svelte', () => ({ accessToken: mocks.accessToken }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: () => ({ getJSON: mocks.getJSON })
}));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createQuery: mocks.createQuery
}));

describe('tag suggestion query lifecycle', () => {
    let client: QueryClient;
    const data: CountResult = { aggregations: {}, total: 0 };

    beforeEach(() => {
        vi.clearAllMocks();
        mocks.accessToken.current = 'session-a';
        mocks.getJSON.mockResolvedValue({ data });
        client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    });
    afterEach(() => client.clear());

    function options(organizationId = 'organization-a', search = '', enabled = true) {
        getTagSuggestionsQuery({ enabled: () => enabled, params: { search }, route: { organizationId } });
        return mocks.createQuery.mock.calls.at(-1)![0]();
    }

    it.each([
        { enabled: false, token: 'session-a' },
        { enabled: true, token: null }
    ])('does not fetch when disabled or unauthenticated: %j', async ({ enabled, token }) => {
        // Arrange
        mocks.accessToken.current = token;
        const observer = new QueryObserver(client, options('organization-a', '', enabled));

        // Act
        const stop = observer.subscribe(() => {});
        await Promise.resolve();

        // Assert
        expect(observer.getCurrentResult().fetchStatus).toBe('idle');
        expect(mocks.getJSON).not.toHaveBeenCalled();
        stop();
    });

    it('reuses fresh results for five minutes across observers', async () => {
        // Arrange
        const initial = options();
        await client.fetchQuery(initial);
        const observer = new QueryObserver(client, options());

        // Act
        const stop = observer.subscribe(() => {});

        // Assert
        expect(observer.getCurrentResult().data).toEqual(data);
        expect(mocks.getJSON).toHaveBeenCalledTimes(1);
        expect(initial.staleTime).toBe(300000);
        stop();
    });

    it('keeps fresh suggestions cached through event notifications', async () => {
        // Arrange
        const initial = options();
        await client.fetchQuery(initial);

        // Act
        await invalidatePersistentEventQueries(client, { change_type: ChangeType.Saved, data: {}, organization_id: 'organization-a', type: 'PersistentEvent' });

        // Assert
        expect(client.getQueryState(initial.queryKey)?.isInvalidated).toBe(false);
        expect(client.getQueryData(initial.queryKey)).toEqual(data);
    });

    it('loads distinct organization data and reuses it only in its own organization', async () => {
        // Arrange
        const otherData: CountResult = { total: 27 };
        const firstOptions = options();
        await client.fetchQuery(firstOptions);
        mocks.getJSON.mockResolvedValueOnce({ data: otherData });
        const observer = new QueryObserver(client, options('organization-b'));

        // Act
        const stop = observer.subscribe(() => {});
        const beforeResponse = observer.getCurrentResult().data;
        await vi.waitFor(() => expect(observer.getCurrentResult().isSuccess).toBe(true));
        const otherResult = observer.getCurrentResult().data;
        observer.setOptions(firstOptions);

        // Assert
        expect(beforeResponse).toBeUndefined();
        expect(otherResult).toEqual(otherData);
        expect(observer.getCurrentResult().data).toEqual(data);
        expect(mocks.getJSON).toHaveBeenCalledTimes(2);
        expect(mocks.getJSON.mock.calls[1]![0]).toBe('/organizations/organization-b/events/count');
        stop();
    });

    it('does not reuse a previous session after logout and login with the same token', async () => {
        // Arrange
        const initial = options();
        await client.fetchQuery(initial);
        mocks.accessToken.current = null;
        options();
        mocks.accessToken.current = 'session-a';
        const nextData: CountResult = { total: 42 };
        mocks.getJSON.mockResolvedValueOnce({ data: nextData });
        const nextOptions = options();
        const observer = new QueryObserver(client, nextOptions);

        // Act
        const stop = observer.subscribe(() => {});
        const beforeResponse = observer.getCurrentResult().data;
        await vi.waitFor(() => expect(observer.getCurrentResult().isSuccess).toBe(true));

        // Assert
        expect(beforeResponse).toBeUndefined();
        expect(observer.getCurrentResult().data).toEqual(nextData);
        expect(mocks.getJSON).toHaveBeenCalledTimes(2);
        expect(nextOptions.queryKey).not.toEqual(initial.queryKey);
        expect(JSON.stringify(nextOptions.queryKey)).not.toContain('session-a');
        stop();
    });

    it('cancels stale requests and never applies their response to a newer search', async () => {
        // Arrange
        const pending = Promise.withResolvers<{ data: CountResult }>();
        mocks.getJSON.mockReturnValueOnce(pending.promise);
        const observer = new QueryObserver(client, options('organization-a', 'older'));
        const stop = observer.subscribe(() => {});
        await vi.waitFor(() => expect(mocks.getJSON).toHaveBeenCalledTimes(1));
        const signal = mocks.getJSON.mock.calls[0]![1].signal as AbortSignal;

        // Act
        observer.setOptions(options('organization-a', 'newer'));
        await vi.waitFor(() => expect(observer.getCurrentResult().isSuccess).toBe(true));
        pending.resolve({ data: { total: 999 } });
        await Promise.resolve();

        // Assert
        expect(signal.aborted).toBe(true);
        expect(observer.getCurrentResult().data).toEqual(data);
        stop();
    });

    it('retains failures for explicit retry and sends no dashboard filters', async () => {
        // Arrange
        mocks.getJSON.mockRejectedValueOnce(new Error('unavailable'));
        const observer = new QueryObserver(client, options());
        const stop = observer.subscribe(() => {});
        await vi.waitFor(() => expect(observer.getCurrentResult().isError).toBe(true));
        const requestsAfterFailure = mocks.getJSON.mock.calls.length;

        // Act
        await observer.refetch();

        // Assert
        expect(requestsAfterFailure).toBe(1);
        expect(mocks.getJSON.mock.calls[0]![1].params).toEqual({ aggregations: 'terms:(tags~251)', time: 'all' });
        expect(observer.getCurrentResult().isSuccess).toBe(true);
        stop();
    });
});
