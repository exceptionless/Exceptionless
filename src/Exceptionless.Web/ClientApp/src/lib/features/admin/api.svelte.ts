import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery } from '@tanstack/svelte-query';

import type { AdminStats, ElasticsearchInfo, ElasticsearchSnapshotsResponse, MigrationsResponse } from './models';

export type RunMaintenanceJobParams = {
    name: string;
    organizationId?: string;
    utcEnd?: Date;
    utcStart?: Date;
};

export const queryKeys = {
    elasticsearch: ['admin', 'elasticsearch'] as const,
    migrations: ['admin', 'migrations'] as const,
    snapshots: ['admin', 'elasticsearch', 'snapshots'] as const,
    stats: ['admin', 'stats'] as const
};

export function getAdminStatsQuery() {
    return createQuery<AdminStats, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<AdminStats>('admin/stats', {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.stats,
        staleTime: 60 * 1000
    }));
}

export function getElasticsearchQuery() {
    return createQuery<ElasticsearchInfo, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ElasticsearchInfo>('admin/elasticsearch', {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.elasticsearch,
        staleTime: 30 * 1000
    }));
}

export function getElasticsearchSnapshotsQuery() {
    return createQuery<ElasticsearchSnapshotsResponse, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ElasticsearchSnapshotsResponse>('admin/elasticsearch/snapshots', {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.snapshots,
        staleTime: 60 * 1000
    }));
}

export function getMigrationsQuery() {
    return createQuery<MigrationsResponse, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<MigrationsResponse>('admin/migrations', {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.migrations,
        staleTime: 30 * 1000
    }));
}

export function runMaintenanceJobMutation() {
    return createMutation<void, ProblemDetails, RunMaintenanceJobParams>(() => ({
        mutationFn: async (params: RunMaintenanceJobParams) => {
            const client = useFetchClient();
            await client.getJSON(`admin/maintenance/${params.name}`, {
                params: {
                    organizationId: params.organizationId,
                    utcEnd: params.utcEnd?.toISOString(),
                    utcStart: params.utcStart?.toISOString()
                }
            });
        }
    }));
}
