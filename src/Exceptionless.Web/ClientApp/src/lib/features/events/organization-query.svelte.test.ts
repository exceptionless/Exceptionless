import type { FetchClientResponse, ProblemDetails } from '@foundatiofx/fetchclient';

import { QueryClient, QueryObserver, type QueryObserverOptions } from '@tanstack/svelte-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { EventSummaryModel, SummaryTemplateKeys } from './components/summary/index';

import { getOrganizationEventsQuery } from './api.svelte';

type EventsResponse = FetchClientResponse<EventSummaryModel<SummaryTemplateKeys>[]>;

const mocks = vi.hoisted(() => ({
    createQuery: vi.fn<(options: () => QueryObserverOptions<EventsResponse, ProblemDetails>) => void>(),
    getJSON: vi.fn()
}));
vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'test-token' } }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: () => ({ getJSON: mocks.getJSON })
}));
vi.mock('@tanstack/svelte-query', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@tanstack/svelte-query')>()),
    createQuery: mocks.createQuery
}));

describe('organization event query transitions', () => {
    let client: QueryClient;
    beforeEach(() => {
        vi.resetAllMocks();
        client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    });
    afterEach(() => client.clear());

    it.each(['summary', 'stack_frequent'] as const)('clears previous %s rows while another organization loads', async (mode) => {
        let organizationId = 'membership';
        let page = 1;
        const firstResponse = { data: [{ id: 'membership-event' }] } as EventsResponse;
        const pending = Promise.withResolvers<EventsResponse>();
        mocks.getJSON.mockResolvedValueOnce(firstResponse).mockReturnValue(pending.promise);
        getOrganizationEventsQuery({
            params: {
                mode,
                get page() {
                    return page;
                }
            },
            route: {
                get organizationId() {
                    return organizationId;
                }
            }
        });
        const options = mocks.createQuery.mock.calls[0]![0];
        const observer = new QueryObserver(client, options());
        const stop = observer.subscribe(() => {});
        try {
            await vi.waitFor(() => expect(observer.getCurrentResult().data).toBe(firstResponse));
            page = 2;
            observer.setOptions(options());
            expect(observer.getCurrentResult().data).toBe(firstResponse);

            organizationId = 'impersonated';
            observer.setOptions(options());
            expect(observer.getCurrentResult().data).toBeUndefined();

            const nextResponse = { data: [{ id: 'impersonated-event' }] } as EventsResponse;
            pending.resolve(nextResponse);
            await vi.waitFor(() => expect(observer.getCurrentResult().data).toBe(nextResponse));
        } finally {
            stop();
        }
    });
});
