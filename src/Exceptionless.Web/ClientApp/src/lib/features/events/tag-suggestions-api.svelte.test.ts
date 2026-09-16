import type { CountResult } from '$shared/models';

import { QueryClient, QueryObserver, type QueryObserverOptions } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { getTagSuggestionsQuery } from './api.svelte';

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
        getTagSuggestionsQuery({ enabled: () => enabled, organizationId, search });
        return mocks.createQuery.mock.calls.at(-1)![0]();
    }

    it('does not query a closed picker or an unauthenticated session', () => {
        expect(options('organization-a', '', false).enabled).toBe(false);
        mocks.accessToken.current = null;
        expect(options().enabled).toBe(false);
    });

    it('reuses fresh results for five minutes and scopes keys to organization, search and session', async () => {
        const initial = options();
        const first = new QueryObserver(client, initial);
        const stop = first.subscribe(() => {});
        await vi.waitFor(() => expect(first.getCurrentResult().isSuccess).toBe(true));
        stop();
        const again = new QueryObserver(client, options());
        const stopAgain = again.subscribe(() => {});
        expect(again.getCurrentResult().data).toEqual(data);
        expect(mocks.getJSON).toHaveBeenCalledTimes(1);
        expect(initial.staleTime).toBe(300000);
        expect(options('organization-b').queryKey).not.toEqual(initial.queryKey);
        expect(options('organization-a', 'rare').queryKey).not.toEqual(initial.queryKey);
        mocks.accessToken.current = 'session-b';
        expect(options().queryKey).not.toEqual(initial.queryKey);
        expect(JSON.stringify(initial.queryKey)).not.toContain('session-a');
        stopAgain();
    });

    it('cancels stale requests and never applies their response to a newer search', async () => {
        const pending = Promise.withResolvers<{ data: CountResult }>();
        mocks.getJSON.mockReturnValueOnce(pending.promise);
        const observer = new QueryObserver(client, options('organization-a', 'older'));
        const stop = observer.subscribe(() => {});
        await vi.waitFor(() => expect(mocks.getJSON).toHaveBeenCalledTimes(1));
        const signal = mocks.getJSON.mock.calls[0]![1].signal as AbortSignal;
        observer.setOptions(options('organization-a', 'newer'));
        await vi.waitFor(() => expect(observer.getCurrentResult().isSuccess).toBe(true));
        expect(signal.aborted).toBe(true);
        pending.resolve({ data: { total: 999 } });
        await Promise.resolve();
        expect(observer.getCurrentResult().data).toEqual(data);
        stop();
    });

    it('retains failures for explicit retry and sends no dashboard filters', async () => {
        mocks.getJSON.mockRejectedValueOnce(new Error('unavailable'));
        const observer = new QueryObserver(client, options());
        const stop = observer.subscribe(() => {});
        await vi.waitFor(() => expect(observer.getCurrentResult().isError).toBe(true));
        expect(mocks.getJSON).toHaveBeenCalledTimes(1);
        expect(mocks.getJSON.mock.calls[0]![1].params).toEqual({ aggregations: 'terms:(tags~251)', time: 'all' });
        await observer.refetch();
        expect(observer.getCurrentResult().isSuccess).toBe(true);
        stop();
    });
});
