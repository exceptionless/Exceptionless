import type { WebSocketMessageValue } from '$features/websockets/models';
import type { QueryClient } from '@tanstack/svelte-query';

import { accessToken } from '$features/auth/index.svelte';
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { Invoice, InvoiceGridModel, NewOrganization, ViewOrganization } from './models';

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
    deleteOrganization: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    id: (id: string | undefined, mode: 'stats' | undefined) => (mode ? ([...queryKeys.type, id, { mode }] as const) : ([...queryKeys.type, id] as const)),
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    invoice: (id: string | undefined) => [...queryKeys.type, 'invoice', id] as const,
    invoices: (organizationId: string | undefined) => [...queryKeys.type, organizationId, 'invoices'] as const,
    list: (mode: 'stats' | undefined) => (mode ? ([...queryKeys.type, 'list', { mode }] as const) : ([...queryKeys.type, 'list'] as const)),
    postOrganization: () => [...queryKeys.type, 'post-organization'] as const,
    type: ['Organization'] as const
};

export interface AddOrganizationUserRequest {
    route: {
        email: string;
        organizationId: string;
    };
}

export interface DeleteOrganizationRequest {
    route: {
        ids: string[];
    };
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
    mode: GetOrganizationsMode;
}

export interface GetOrganizationsRequest {
    params?: GetOrganizationsParams;
}

export interface RemoveOrganizationUserRequest {
    route: {
        email: string;
        organizationId: string;
    };
}

export interface UpdateOrganizationRequest {
    route: {
        id: string;
    };
}

export function addOrganizationUser(request: AddOrganizationUserRequest) {
    const queryClient = useQueryClient();
    return createMutation<{ emailAddress: string }, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && !!request.route.email,
        mutationFn: async () => {
            const client = useFetchClient();
            const response = await client.postJSON<{ emailAddress: string }>(
                `organizations/${request.route.organizationId}/users/${encodeURIComponent(request.route.email)}`
            );
            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.organizationId, undefined) });
            queryClient.invalidateQueries({ queryKey: ['User', 'organization', request.route.organizationId] });
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
        onSuccess: (data: ViewOrganization[]) => {
            data.forEach((organization) => {
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
        queryKey: [...queryKeys.list(request.params?.mode ?? undefined), { params: request.params }]
    }));
}

export function postOrganization() {
    const queryClient = useQueryClient();

    return createMutation<ViewOrganization, ProblemDetails, NewOrganization>(() => ({
        enabled: () => !!accessToken.current,
        mutationFn: async (organization: NewOrganization) => {
            const client = useFetchClient();
            const response = await client.postJSON<ViewOrganization>('organizations', organization);
            return response.data!;
        },
        mutationKey: queryKeys.postOrganization(),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.type });
        }
    }));
}

export function removeOrganizationUser(request: RemoveOrganizationUserRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && !!request.route.email,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.deleteJSON<void>(`organizations/${request.route.organizationId}/users/${encodeURIComponent(request.route.email)}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.id(request.route.organizationId, undefined) });
            queryClient.invalidateQueries({ queryKey: ['User', 'organization', request.route.organizationId] });
        }
    }));
}

export function updateOrganization(request: UpdateOrganizationRequest) {
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
        onSuccess: (organization: ViewOrganization) => {
            queryClient.setQueryData(queryKeys.id(request.route.id, 'stats'), organization);
            queryClient.setQueryData(queryKeys.id(request.route.id, undefined), organization);
        }
    }));
}
