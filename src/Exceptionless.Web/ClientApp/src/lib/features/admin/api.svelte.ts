import { type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type {
    AdminStats,
    ElasticsearchInfo,
    ElasticsearchSnapshotsResponse,
    MigrationsResponse,
    OAuthApplication,
    OAuthApplicationRequest,
    PredefinedSavedViewDefinition
} from './models';

export type RunMaintenanceJobParams = {
    name: string;
    organizationId?: string;
    utcEnd?: Date;
    utcStart?: Date;
};

export const queryKeys = {
    elasticsearch: ['admin', 'elasticsearch'] as const,
    migrations: ['admin', 'migrations'] as const,
    oauthApplications: ['admin', 'oauth-applications'] as const,
    snapshots: ['admin', 'elasticsearch', 'snapshots'] as const,
    stats: ['admin', 'stats'] as const
};

export function deleteOAuthApplicationMutation() {
    const queryClient = useQueryClient();

    return createMutation<void, ProblemDetails, string>(() => ({
        mutationFn: async (id: string) => {
            const client = useFetchClient();
            const response = await client.delete(`admin/oauth-applications/${id}`);

            if (!response.ok) {
                throw response.problem;
            }
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.oauthApplications });
        }
    }));
}

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

export function getOAuthApplicationsQuery() {
    return createQuery<OAuthApplication[], ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<OAuthApplication[]>('admin/oauth-applications', {
                signal
            });

            if (!response.ok) {
                throw response.problem;
            }

            return response.data ?? [];
        },
        queryKey: queryKeys.oauthApplications,
        staleTime: 30 * 1000
    }));
}

export function getOrgSavedViewsExportMutation() {
    return createMutation<string, ProblemDetails, string>(() => ({
        mutationFn: async (organizationId: string) => {
            const client = useFetchClient();
            const response = await client.getJSON<PredefinedSavedViewDefinition[]>(`organizations/${organizationId}/saved-views/export`);

            return JSON.stringify(response.data ?? [], null, 2);
        }
    }));
}

export function getPredefinedSavedViewsMutation() {
    return createMutation<string, ProblemDetails, void>(() => ({
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<PredefinedSavedViewDefinition[]>('saved-views/predefined');

            return JSON.stringify(response.data ?? [], null, 2);
        }
    }));
}

export function postForceUpdatePredefinedSavedViewsMutation() {
    return createMutation<void, ProblemDetails, void>(() => ({
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.post('saved-views/predefined/force-update');

            if (!response.ok) {
                throw response.problem;
            }
        }
    }));
}

export function postOAuthApplicationMutation() {
    const queryClient = useQueryClient();

    return createMutation<OAuthApplication, ProblemDetails, OAuthApplicationRequest>(() => ({
        mutationFn: async (request: OAuthApplicationRequest) => {
            const client = useFetchClient();
            const response = await client.postJSON<OAuthApplication>('admin/oauth-applications', request);

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.oauthApplications });
        }
    }));
}

export function putOAuthApplicationMutation() {
    const queryClient = useQueryClient();

    return createMutation<OAuthApplication, ProblemDetails, { id: string; request: OAuthApplicationRequest }>(() => ({
        mutationFn: async ({ id, request }) => {
            const client = useFetchClient();
            const response = await client.putJSON<OAuthApplication>(`admin/oauth-applications/${id}`, request);

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.oauthApplications });
        }
    }));
}

export function putPredefinedSavedViewsMutation() {
    return createMutation<string, ProblemDetails, string>(() => ({
        mutationFn: async (json: string) => {
            const client = useFetchClient();
            const definitions = JSON.parse(json) as PredefinedSavedViewDefinition[];
            const response = await client.putJSON<PredefinedSavedViewDefinition[]>('saved-views/predefined', definitions);

            return JSON.stringify(response.data ?? [], null, 2);
        }
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
