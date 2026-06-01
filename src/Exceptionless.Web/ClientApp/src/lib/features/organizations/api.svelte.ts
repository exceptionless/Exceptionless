import type { WebSocketMessageValue } from '$features/websockets/models';
import type { BillingPlan, ChangePlanRequest, ChangePlanResult, StringValueFromBody } from '$lib/generated/api';
import type { QueryClient } from '@tanstack/svelte-query';

import { accessToken } from '$features/auth/index.svelte';
import { fetchApiJson } from '$features/shared/api/api.svelte';
import { queryKeys as userQueryKeys } from '$features/users/api.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { Invoice, InvoiceGridModel, NewOrganization, SuspensionCode, ViewOrganization } from './models';

export async function invalidateOrganizationQueries(queryClient: QueryClient, message: WebSocketMessageValue<'OrganizationChanged'>) {
    const { id } = message;
    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id, undefined) });
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id, 'stats') });

        // Invalidate regardless of mode
        await queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
    } else {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}

export const queryKeys = {
    adminSearch: (params: GetAdminSearchOrganizationsParams) => [...queryKeys.list(params.mode), 'admin', { ...params }] as const,
    changePlan: (id: string | undefined) => [...queryKeys.type, id, 'change-plan'] as const,
    data: (id: string | undefined) => [...queryKeys.type, id, 'data'] as const,
    deleteOrganization: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    icon: (id: string | undefined) => [...queryKeys.id(id, undefined), 'icon'] as const,
    id: (id: string | undefined, mode: 'stats' | undefined) => (mode ? ([...queryKeys.type, id, { mode }] as const) : ([...queryKeys.type, id] as const)),
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    invoice: (id: string | undefined) => [...queryKeys.type, 'invoice', id] as const,
    invoices: (id: string | undefined) => [...queryKeys.type, id, 'invoices'] as const,
    list: (mode: 'stats' | undefined) => (mode ? ([...queryKeys.type, 'list', { mode }] as const) : ([...queryKeys.type, 'list'] as const)),
    plans: (id: string | undefined) => [...queryKeys.type, id, 'plans'] as const,
    postOrganization: () => [...queryKeys.type, 'post-organization'] as const,
    setBonusOrganization: (id: string | undefined) => [...queryKeys.type, id, 'set-bonus'] as const,
    setFeature: (id: string | undefined) => [...queryKeys.type, id, 'set-feature'] as const,
    suspendOrganization: (id: string | undefined) => [...queryKeys.type, id, 'suspend'] as const,
    type: ['Organization'] as const,
    unsuspendOrganization: (id: string | undefined) => [...queryKeys.type, id, 'unsuspend'] as const
};

export interface AddOrganizationUserRequest {
    route: {
        organizationId: string;
    };
}

export interface ChangePlanMutationRequest {
    route: {
        organizationId: string;
    };
}

export interface DeleteOrganizationDataParams {
    key: string;
}

export interface DeleteOrganizationDataRequest {
    route: {
        id: string | undefined;
    };
}

export interface DeleteOrganizationRequest {
    route: {
        ids: string[];
    };
}

export interface DeleteOrganizationUserRequest {
    route: {
        email: string;
        organizationId: string;
    };
}

export interface DeleteSuspendOrganizationRequest {
    route: {
        id: string | undefined;
    };
}

export interface GetAdminSearchOrganizationsParams {
    criteria?: string;
    limit?: number;
    mode?: 'stats' | undefined;
    page?: number;
    paid?: boolean;
    suspended?: boolean;
}

export interface GetAdminSearchOrganizationsRequest {
    params?: GetAdminSearchOrganizationsParams;
}

export interface GetInvoiceRequest {
    route: {
        id: string;
    };
}

export interface GetInvoicesRequest {
    params?: {
        after?: string;
        before?: string;
        limit?: number;
    };
    route: {
        organizationId: string;
    };
}

// TODO: Look at params:?
export interface GetOrganizationRequest {
    params?: {
        mode: 'stats' | undefined;
    };
    route: {
        id: string | undefined;
    };
}

export type GetOrganizationsMode = 'stats' | null;

export interface GetOrganizationsParams {
    filter?: string;
    mode: GetOrganizationsMode;
}

export interface GetOrganizationsRequest {
    params?: GetOrganizationsParams;
}

export interface GetPlansRequest {
    route: {
        organizationId: string;
    };
}

export interface OrganizationIconRequest {
    route: {
        id: string | undefined;
    };
}

export interface PatchOrganizationRequest {
    route: {
        id: string;
    };
}

export interface PostOrganizationDataParams {
    key: string;
    value: string;
}

export interface PostOrganizationDataRequest {
    route: {
        id: string | undefined;
    };
}

export interface PostSetBonusOrganizationParams {
    bonusEvents: number;
    expires?: Date;
    organizationId: string;
}

export interface PostSuspendOrganizationParams {
    code: SuspensionCode;
    notes?: string;
}

export interface PostSuspendOrganizationRequest {
    route: {
        id: string | undefined;
    };
}

export interface SetOrganizationFeatureRequest {
    route: {
        id: string | undefined;
    };
}

export function addOrganizationUser(request: AddOrganizationUserRequest) {
    const queryClient = useQueryClient();
    return createMutation<{ emailAddress: string }, ProblemDetails, string>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (email: string) => {
            const client = useFetchClient();
            const response = await client.postJSON<{ emailAddress: string }>(
                `organizations/${request.route.organizationId}/users/${encodeURIComponent(email)}`
            );
            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.organizationId, undefined) });
            // Also invalidate the user list for this org (different query namespace from Organization)
            queryClient.invalidateQueries({ queryKey: ['User', 'organization', request.route.organizationId] });
        }
    }));
}

export function changePlanMutation(request: ChangePlanMutationRequest) {
    const queryClient = useQueryClient();
    return createMutation<ChangePlanResult, ProblemDetails, ChangePlanRequest>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (params: ChangePlanRequest) => {
            const client = useFetchClient();
            const response = await client.postJSON<ChangePlanResult>(`organizations/${request.route.organizationId}/change-plan`, params);
            return response.data!;
        },
        mutationKey: queryKeys.changePlan(request.route.organizationId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.organizationId, undefined) });
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.organizationId, 'stats') });
            queryClient.invalidateQueries({ queryKey: queryKeys.plans(request.route.organizationId) });
        }
    }));
}

export function deleteOrganization(request: DeleteOrganizationRequest) {
    const queryClient = useQueryClient();

    return createMutation<FetchClientResponse<unknown>, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.ids?.length,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.delete(`organizations/${request.route.ids?.join(',')}`, {
                expectedStatusCodes: [202]
            });

            return response;
        },
        mutationKey: queryKeys.deleteOrganization(request.route.ids),
        onError: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id, undefined) }));
        },
        onSuccess: () => {
            request.route.ids?.forEach((id) => queryClient.invalidateQueries({ queryKey: queryKeys.id(id, undefined) }));
        }
    }));
}

export function deleteOrganizationIcon(request: OrganizationIconRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewOrganization, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async () => {
            return await fetchApiJson<ViewOrganization>(`organizations/${request.route.id}/icon`, {
                method: 'DELETE'
            });
        },
        mutationKey: queryKeys.icon(request.route.id),
        onSuccess: (organization: ViewOrganization) => updateOrganizationCache(queryClient, request.route.id, organization)
    }));
}

export function deleteOrganizationData(request: DeleteOrganizationDataRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, DeleteOrganizationDataParams>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async ({ key }: DeleteOrganizationDataParams) => {
            const client = useFetchClient();
            const response = await client.delete(`organizations/${request.route.id}/data/${encodeURIComponent(key)}`);
            return response.ok;
        },
        mutationKey: queryKeys.data(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
        },
        onSuccess: (_, { key }) => {
            updateOrganizationQueryData(queryClient, request.route.id, (organization) => {
                if (!organization.data) {
                    return organization;
                }

                const data = { ...organization.data };
                delete data[key];

                return { ...organization, data };
            });
        }
    }));
}
export function deleteOrganizationUser(request: DeleteOrganizationUserRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && !!request.route.email,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.deleteJSON<void>(`organizations/${request.route.organizationId}/users/${encodeURIComponent(request.route.email)}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.organizationId, undefined) });
            // Also invalidate the user list for this org (different query namespace from Organization)
            queryClient.invalidateQueries({ queryKey: ['User', 'organization', request.route.organizationId] });
        }
    }));
}

export function deleteSuspendOrganization(request: DeleteSuspendOrganizationRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.delete(`organizations/${request.route.id}/suspend`);
            return response.ok;
        },
        mutationKey: queryKeys.unsuspendOrganization(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, 'stats') });
            queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
        }
    }));
}

export function getAdminOrganizationsQuery(request: GetAdminSearchOrganizationsRequest) {
    return createQuery<FetchClientResponse<ViewOrganization[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewOrganization[]>('admin/organizations', {
                params: {
                    criteria: request.params?.criteria,
                    limit: request.params?.limit ?? 10,
                    mode: request.params?.mode,
                    page: request.params?.page ?? 1,
                    paid: request.params?.paid,
                    suspended: request.params?.suspended
                },
                signal
            });

            return response;
        },
        queryKey: queryKeys.adminSearch(request.params ?? {})
    }));
}

export function getInvoiceQuery(request: GetInvoiceRequest) {
    const queryClient = useQueryClient();

    return createQuery<Invoice, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Invoice>(`organizations/invoice/${request.route.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.invoice(request.route.id)
    }));
}

export function getInvoicesQuery(request: GetInvoicesRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<InvoiceGridModel[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<InvoiceGridModel[]>(`organizations/${request.route.organizationId}/invoices`, {
                expectedStatusCodes: [200, 404],
                params: { ...request.params },
                signal
            });

            return response;
        },
        queryKey: queryKeys.invoices(request.route.organizationId)
    }));
}

export function getOrganizationQuery(request: GetOrganizationRequest) {
    const queryClient = useQueryClient();

    return createQuery<ViewOrganization, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        onSuccess: (data: ViewOrganization) => {
            if (request.params?.mode) {
                queryClient.setQueryData(queryKeys.id(request.route.id, request.params.mode), data);
            }

            queryClient.setQueryData(queryKeys.id(request.route.id!, undefined), data);
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewOrganization>(`organizations/${request.route.id}`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.id(request.route.id, request.params?.mode)
    }));
}

export function getOrganizationsQuery(request: GetOrganizationsRequest) {
    const queryClient = useQueryClient();

    return createQuery<FetchClientResponse<ViewOrganization[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        onSuccess: (data: FetchClientResponse<ViewOrganization[]>) => {
            data.data?.forEach((organization) => {
                if (request.params?.mode) {
                    queryClient.setQueryData(queryKeys.id(organization.id!, request.params.mode), organization);
                }

                queryClient.setQueryData(queryKeys.id(organization.id!, undefined), organization);
            });
        },
        queryClient,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ViewOrganization[]>('organizations', {
                params: { ...request.params },
                signal
            });

            return response;
        },
        queryKey: [...queryKeys.list(request.params?.mode ?? undefined), { params: { ...request.params } }]
    }));
}

/**
 * Query to fetch available billing plans for an organization.
 */
export function getPlansQuery(request: GetPlansRequest) {
    return createQuery<BillingPlan[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<BillingPlan[]>(`organizations/${request.route.organizationId}/plans`, {
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.plans(request.route.organizationId)
    }));
}

export function patchOrganization(request: PatchOrganizationRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewOrganization, ProblemDetails, NewOrganization>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (data: NewOrganization) => {
            const client = useFetchClient();
            const response = await client.patchJSON<ViewOrganization>(`organizations/${request.route.id}`, data);
            return response.data!;
        },
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
        },
        onSuccess: (organization: ViewOrganization) => updateOrganizationCache(queryClient, request.route.id, organization)
    }));
}

export function postOrganization() {
    const queryClient = useQueryClient();
    const defaultOrganizationsQueryKey = [...queryKeys.list(undefined), { params: {} }] as const;

    return createMutation<ViewOrganization, ProblemDetails, NewOrganization>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (organization: NewOrganization) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewOrganization>('organizations', organization);
            return response.data!;
        },
        mutationKey: queryKeys.postOrganization(),
        onSuccess: async (organization: ViewOrganization) => {
            queryClient.setQueryData(queryKeys.id(organization.id, 'stats'), organization);
            queryClient.setQueryData(queryKeys.id(organization.id, undefined), organization);

            queryClient.setQueryData<FetchClientResponse<ViewOrganization[]> | undefined>(defaultOrganizationsQueryKey, (response) => {
                if (!response || response.data?.some((existingOrganization) => existingOrganization.id === organization.id)) {
                    return response;
                }

                return {
                    ...response,
                    data: [...(response.data ?? []), organization]
                };
            });

            await Promise.all([
                queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) }),
                queryClient.invalidateQueries({ queryKey: userQueryKeys.me() })
            ]);
        }
    }));
}

export function postOrganizationData(request: PostOrganizationDataRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, PostOrganizationDataParams>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async ({ key, value }: PostOrganizationDataParams) => {
            const client = useFetchClient();
            const response = await client.post(`organizations/${request.route.id}/data/${encodeURIComponent(key)}`, <StringValueFromBody>{ value });
            return response.ok;
        },
        mutationKey: queryKeys.data(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
        },
        onSuccess: (_, { key, value }) => {
            updateOrganizationQueryData(queryClient, request.route.id, (organization) => ({
                ...organization,
                data: {
                    ...(organization.data ?? {}),
                    [key]: value
                }
            }));
        }
    }));
}

export function postSetBonusOrganization() {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, PostSetBonusOrganizationParams>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (params: PostSetBonusOrganizationParams) => {
            const client = useFetchClient();

            const response = await client.post('admin/set-bonus', undefined, {
                params: {
                    ...params,
                    expires: params.expires ? params.expires.toISOString() : undefined
                }
            });
            return response.ok;
        },
        mutationKey: queryKeys.setBonusOrganization(undefined),
        onError: (error, params) => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(params.organizationId, undefined) });
        },
        onSuccess: (data, params) => {
            // TODO: Normalize all this invalidation.
            queryClient.invalidateQueries({ queryKey: queryKeys.id(params.organizationId, undefined) });
            queryClient.invalidateQueries({ queryKey: queryKeys.id(params.organizationId, 'stats') });
            queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
        }
    }));
}

export function postSuspendOrganization(request: PostSuspendOrganizationRequest) {
    const queryClient = useQueryClient();

    return createMutation<boolean, ProblemDetails, PostSuspendOrganizationParams>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (params: PostSuspendOrganizationParams) => {
            const client = useFetchClient();
            const response = await client.postJSON(`organizations/${request.route.id}/suspend`, params);
            return response.ok;
        },
        mutationKey: queryKeys.suspendOrganization(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
        },
        onSuccess: () => {
            // TODO: Normalize all this invalidation.
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, 'stats') });
            queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
        }
    }));
}

export function removeOrganizationFeature(request: SetOrganizationFeatureRequest) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, string>(() => ({
        mutationFn: async (feature: string) => {
            if (!request.route.id) {
                throw new Error('Organization ID is required');
            }

            const client = useFetchClient();
            const response = await client.delete(`organizations/${request.route.id}/features/${feature}`);
            return response.ok;
        },
        mutationKey: queryKeys.setFeature(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
            queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
        }
    }));
}

export function setOrganizationFeature(request: SetOrganizationFeatureRequest) {
    const queryClient = useQueryClient();
    return createMutation<boolean, ProblemDetails, string>(() => ({
        mutationFn: async (feature: string) => {
            if (!request.route.id) {
                throw new Error('Organization ID is required');
            }

            const client = useFetchClient();
            const response = await client.post(`organizations/${request.route.id}/features/${feature}`);
            return response.ok;
        },
        mutationKey: queryKeys.setFeature(request.route.id),
        onError: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.id, undefined) });
            queryClient.invalidateQueries({ queryKey: queryKeys.list(undefined) });
        }
    }));
}

export function uploadOrganizationIcon(request: OrganizationIconRequest) {
    const queryClient = useQueryClient();

    return createMutation<ViewOrganization, ProblemDetails, File>(() => ({
        enabled: () => !!accessToken.current && !!request.route.id,
        mutationFn: async (file: File) => {
            const data = new FormData();
            data.append('file', file);
            return await fetchApiJson<ViewOrganization>(`organizations/${request.route.id}/icon`, {
                body: data,
                method: 'POST'
            });
        },
        mutationKey: queryKeys.icon(request.route.id),
        onSuccess: (organization: ViewOrganization) => updateOrganizationCache(queryClient, request.route.id, organization)
    }));
}

function updateOrganizationCache(queryClient: QueryClient, id: string | undefined, organization: ViewOrganization) {
    queryClient.setQueryData(queryKeys.id(id, 'stats'), organization);
    queryClient.setQueryData(queryKeys.id(id, undefined), organization);
    queryClient.setQueriesData<FetchClientResponse<ViewOrganization[]> | undefined>({ queryKey: queryKeys.type }, (response) => {
        if (!Array.isArray(response?.data) || !response.data.some((existingOrganization) => existingOrganization.id === organization.id)) {
            return response;
        }

        return {
            ...response,
            data: response.data.map((existingOrganization) => {
                return existingOrganization.id === organization.id ? organization : existingOrganization;
            })
        };
    });
}

function updateOrganizationQueryData(
    queryClient: ReturnType<typeof useQueryClient>,
    id: string | undefined,
    updater: (organization: ViewOrganization) => ViewOrganization
) {
    // Keep both cached variants in sync because the same organization can be loaded with or without stats mode.
    for (const mode of [undefined, 'stats'] as const) {
        queryClient.setQueryData<undefined | ViewOrganization>(queryKeys.id(id, mode), (organization) => (organization ? updater(organization) : organization));
    }
}
