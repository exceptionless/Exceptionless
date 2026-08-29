import { invalidateAssistantAccessQueries } from '$features/assistant/api.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type {
    AdminAssistantSettings,
    AdminAssistantUsage,
    AdminEventSubmissionSettings,
    AdminStats,
    ElasticsearchInfo,
    ElasticsearchSnapshotsResponse,
    MigrationsResponse,
    OAuthApplication,
    OAuthApplicationRequest,
    PredefinedSavedViewDefinition,
    ProductTourUsageResponse,
    UpdateAssistantEnabledSettingsRequest,
    UpdateAssistantSettingsRequest,
    UpdateEventSubmissionSettingsRequest
} from './models';

export type GetOAuthApplicationsParams = {
    criteria?: string;
    limit?: number;
    organization?: string;
    page?: number;
};

export type GetOAuthApplicationsRequest = {
    params?: GetOAuthApplicationsParams;
};

export type RunMaintenanceJobParams = {
    name: string;
    organizationId?: string;
    utcEnd?: Date;
    utcStart?: Date;
};

export const queryKeys = {
    assistantSettings: ['admin', 'assistant-settings'] as const,
    assistantUsage: (month: string) => ['admin', 'assistant-usage', month] as const,
    elasticsearch: ['admin', 'elasticsearch'] as const,
    eventSubmissionSettings: ['admin', 'event-submission-settings'] as const,
    migrations: ['admin', 'migrations'] as const,
    oauthApplication: (id: string | undefined) => [...queryKeys.oauthApplications, id] as const,
    oauthApplications: ['admin', 'oauth-applications'] as const,
    productTourUsage: (month?: string) => ['admin', 'product-tour-usage', month ?? 'all'] as const,
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
            queryClient.invalidateQueries({
                queryKey: queryKeys.oauthApplications
            });
        }
    }));
}

export function getAdminAssistantSettingsQuery() {
    return createQuery<AdminAssistantSettings, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<AdminAssistantSettings>('admin/assistant-settings', {
                signal
            });

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        queryKey: queryKeys.assistantSettings,
        staleTime: 30 * 1000
    }));
}

export function getAdminAssistantUsageQuery(month: () => string) {
    return createQuery<AdminAssistantUsage, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<AdminAssistantUsage>('admin/assistant-usage', {
                params: {
                    limit: 500,
                    month: `${month()}-01`
                },
                signal
            });

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        queryKey: queryKeys.assistantUsage(month()),
        staleTime: 60 * 1000
    }));
}

export function getAdminProductTourUsageQuery(month: () => string | undefined) {
    return createQuery<ProductTourUsageResponse, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const selectedMonth = month();
            const params = selectedMonth
                ? {
                      limit: 100,
                      month: `${selectedMonth}-01`
                  }
                : {
                      all: true,
                      limit: 100
                  };
            const response = await client.getJSON<ProductTourUsageResponse>('admin/product-tour-usage', {
                params,
                signal
            });

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        queryKey: queryKeys.productTourUsage(month()),
        staleTime: 60 * 1000
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

export function getEventSubmissionSettingsQuery() {
    return createQuery<AdminEventSubmissionSettings, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<AdminEventSubmissionSettings>('admin/event-submission-settings', {
                signal
            });

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        queryKey: queryKeys.eventSubmissionSettings,
        staleTime: 30 * 1000
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

export function getOAuthApplicationQuery(id: () => string | undefined) {
    return createQuery<OAuthApplication, ProblemDetails>(() => ({
        enabled: () => !!id(),
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<OAuthApplication>(`admin/oauth-applications/${id()}`, {
                signal
            });

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        queryKey: queryKeys.oauthApplication(id()),
        staleTime: 30 * 1000
    }));
}

export function getOAuthApplicationsQuery(request: GetOAuthApplicationsRequest = {}) {
    return createQuery<FetchClientResponse<OAuthApplication[]>, ProblemDetails>(() => ({
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<OAuthApplication[]>('admin/oauth-applications', {
                params: {
                    ...request.params
                },
                signal
            });

            if (!response.ok) {
                throw response.problem;
            }

            return response;
        },
        queryKey: [
            ...queryKeys.oauthApplications,
            {
                params: {
                    ...request.params
                }
            }
        ],
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
            queryClient.invalidateQueries({
                queryKey: queryKeys.oauthApplications
            });
        }
    }));
}

export function putAdminAssistantEnabledSettingsMutation() {
    const queryClient = useQueryClient();

    return createMutation<AdminAssistantSettings, ProblemDetails, UpdateAssistantEnabledSettingsRequest>(() => ({
        mutationFn: async (request) => {
            const client = useFetchClient();
            const response = await client.putJSON<AdminAssistantSettings>('admin/assistant-settings/enabled', request);

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        onSuccess: async (settings) => {
            queryClient.setQueryData(queryKeys.assistantSettings, settings);
            await invalidateAssistantAccessQueries(queryClient);
        }
    }));
}

export function putAdminAssistantSettingsMutation() {
    const queryClient = useQueryClient();

    return createMutation<AdminAssistantSettings, ProblemDetails, UpdateAssistantSettingsRequest>(() => ({
        mutationFn: async (request) => {
            const client = useFetchClient();
            const response = await client.putJSON<AdminAssistantSettings>('admin/assistant-settings', request);

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        onSuccess: (settings) => {
            queryClient.setQueryData(queryKeys.assistantSettings, settings);
        }
    }));
}

export function putEventSubmissionSettingsMutation() {
    const queryClient = useQueryClient();

    return createMutation<AdminEventSubmissionSettings, ProblemDetails, UpdateEventSubmissionSettingsRequest>(() => ({
        mutationFn: async (request) => {
            const client = useFetchClient();
            const response = await client.putJSON<AdminEventSubmissionSettings>('admin/event-submission-settings', request);

            if (!response.ok) {
                throw response.problem;
            }

            return response.data!;
        },
        onSuccess: (settings) => {
            queryClient.setQueryData(queryKeys.eventSubmissionSettings, settings);
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
            queryClient.invalidateQueries({
                queryKey: queryKeys.oauthApplications
            });
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
