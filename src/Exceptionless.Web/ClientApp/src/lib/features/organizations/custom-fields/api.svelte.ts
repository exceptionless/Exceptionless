import type { ProblemDetails } from '@exceptionless/fetchclient';

import { accessToken } from '$features/auth/index.svelte';
import { useFetchClient } from '@exceptionless/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { CustomFieldDefinition, NewCustomFieldDefinition, UpdateCustomFieldDefinition } from './models';

interface ApiCustomFieldDefinition {
    created_utc: string;
    description?: string;
    display_order: number;
    id: string;
    index_type: string;
    name: string;
    updated_utc: string;
}

interface ApiNewCustomFieldDefinition {
    description?: string;
    display_order?: number;
    index_type: string;
    name: string;
}

interface ApiUpdateCustomFieldDefinition {
    description?: string;
    display_order?: number;
}

export const queryKeys = {
    customFields: (organizationId: string | undefined) => ['Organization', organizationId, 'custom-fields'] as const,
    type: ['CustomField'] as const
};

export interface CreateCustomFieldRequest {
    route: {
        organizationId: string;
    };
}

export interface DeleteCustomFieldRequest {
    route: {
        fieldId: string;
        organizationId: string;
    };
}

export interface GetCustomFieldsRequest {
    route: {
        organizationId: string | undefined;
    };
}

export interface UpdateCustomFieldRequest {
    route: {
        fieldId: string;
        organizationId: string;
    };
}

export function createCustomFieldMutation(request: CreateCustomFieldRequest) {
    const queryClient = useQueryClient();
    return createMutation<CustomFieldDefinition, ProblemDetails, NewCustomFieldDefinition>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        mutationFn: async (data: NewCustomFieldDefinition) => {
            const client = useFetchClient();
            const response = await client.postJSON<ApiCustomFieldDefinition>(
                `organizations/${request.route.organizationId}/event-custom-fields`,
                mapNewFieldRequest(data)
            );
            return mapApiDefinition(response.data!);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.customFields(request.route.organizationId) });
        }
    }));
}

export function deleteCustomFieldMutation(request: DeleteCustomFieldRequest) {
    const queryClient = useQueryClient();
    return createMutation<void, ProblemDetails, void>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && !!request.route.fieldId,
        mutationFn: async () => {
            const client = useFetchClient();
            await client.delete(`organizations/${request.route.organizationId}/event-custom-fields/${request.route.fieldId}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.customFields(request.route.organizationId) });
        }
    }));
}

export function getCustomFieldsQuery(request: GetCustomFieldsRequest) {
    return createQuery<CustomFieldDefinition[], ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<ApiCustomFieldDefinition[]>(`organizations/${request.route.organizationId}/event-custom-fields`, { signal });
            return response.data?.map(mapApiDefinition) ?? [];
        },
        queryKey: queryKeys.customFields(request.route.organizationId)
    }));
}

export function updateCustomFieldMutation(request: UpdateCustomFieldRequest) {
    const queryClient = useQueryClient();
    return createMutation<CustomFieldDefinition, ProblemDetails, UpdateCustomFieldDefinition>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId && !!request.route.fieldId,
        mutationFn: async (data: UpdateCustomFieldDefinition) => {
            const client = useFetchClient();
            const response = await client.patchJSON<ApiCustomFieldDefinition>(
                `organizations/${request.route.organizationId}/event-custom-fields/${request.route.fieldId}`,
                mapUpdateFieldRequest(data)
            );
            return mapApiDefinition(response.data!);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.customFields(request.route.organizationId) });
        }
    }));
}

function mapApiDefinition(definition: ApiCustomFieldDefinition): CustomFieldDefinition {
    return {
        createdUtc: definition.created_utc,
        description: definition.description,
        displayOrder: definition.display_order,
        id: definition.id,
        indexType: definition.index_type,
        name: definition.name,
        updatedUtc: definition.updated_utc
    };
}

function mapNewFieldRequest(data: NewCustomFieldDefinition): ApiNewCustomFieldDefinition {
    return {
        description: data.description,
        display_order: data.displayOrder,
        index_type: data.indexType,
        name: data.name
    };
}

function mapUpdateFieldRequest(data: UpdateCustomFieldDefinition): ApiUpdateCustomFieldDefinition {
    return {
        description: data.description,
        display_order: data.displayOrder
    };
}
