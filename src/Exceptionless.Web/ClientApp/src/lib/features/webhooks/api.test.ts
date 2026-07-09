import type { FetchClientResponse } from '@exceptionless/fetchclient';

import { ChangeType } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { Webhook } from './models';

import { invalidateWebhookQueries, queryKeys, removeWebhooksFromCaches, syncWebhookCaches, WEBHOOK_REFRESH_DELAY_MS } from './api.svelte';

vi.mock('$features/auth/index.svelte', () => ({
    accessToken: { current: 'test-token' }
}));

const webhook: Webhook = {
    created_utc: '2026-07-09T00:00:00Z',
    event_types: ['NewError'],
    id: '537650f3b77efe23a47914f5',
    is_enabled: true,
    organization_id: '537650f3b77efe23a47914f3',
    project_id: '537650f3b77efe23a47914f4',
    url: 'https://example.com/exceptionless',
    version: '2.0.0'
};

function response(data: Webhook[]): FetchClientResponse<Webhook[]> {
    return { data } as unknown as FetchClientResponse<Webhook[]>;
}

afterEach(() => {
    vi.useRealTimers();
});

describe('webhook caches', () => {
    it('keeps a newly created webhook visible until Elasticsearch refreshes', async () => {
        vi.useFakeTimers();
        const queryClient = new QueryClient();
        const listKey = [...queryKeys.project(webhook.project_id), { params: {} }] as const;
        queryClient.setQueryData(listKey, response([]));
        const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries').mockImplementation(async () => {});

        syncWebhookCaches(queryClient, webhook);
        await invalidateWebhookQueries(queryClient, {
            change_type: ChangeType.Added,
            data: {},
            id: webhook.id,
            organization_id: webhook.organization_id,
            project_id: webhook.project_id,
            type: 'Webhook'
        });

        expect(queryClient.getQueryData<FetchClientResponse<Webhook[]>>(listKey)?.data).toEqual([webhook]);
        expect(invalidateSpy).not.toHaveBeenCalled();

        await vi.advanceTimersByTimeAsync(WEBHOOK_REFRESH_DELAY_MS);
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.id(webhook.id) });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.project(webhook.project_id) });
    });

    it('removes a deleted webhook from cached lists immediately', () => {
        const queryClient = new QueryClient();
        const listKey = [...queryKeys.project(webhook.project_id), { params: {} }] as const;
        queryClient.setQueryData(listKey, response([webhook]));
        queryClient.setQueryData(queryKeys.id(webhook.id), webhook);

        removeWebhooksFromCaches(queryClient, [webhook.id]);

        expect(queryClient.getQueryData<FetchClientResponse<Webhook[]>>(listKey)?.data).toEqual([]);
        expect(queryClient.getQueryData(queryKeys.id(webhook.id))).toBeUndefined();
    });
});
