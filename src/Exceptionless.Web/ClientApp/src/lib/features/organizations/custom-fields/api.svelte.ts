import type {
    CustomFieldDefinitionResponse as ApiCustomFieldDefinition,
    NewCustomFieldDefinition as ApiNewCustomFieldDefinition,
    UpdateCustomFieldDefinition as ApiUpdateCustomFieldDefinition
} from '$lib/generated/api';
import type { ProblemDetails } from '@foundatiofx/fetchclient';

import { accessToken } from '$features/auth/index.svelte';
import { useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import { type CustomFieldDefinition, type NewCustomFieldDefinition, parseApiIndexType, type UpdateCustomFieldDefinition } from './models';

export const CUSTOM_FIELD_QUERY_STALE_TIME_MS = 5 * 60 * 1000;

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
            queryClient.invalidateQueries({
                queryKey: queryKeys.customFields(request.route.organizationId)
            });
        }
    }));
}

export function createCustomFieldsQueryOptions(request: GetCustomFieldsRequest) {
    const organizationId = request.route.organizationId;

    return {
        enabled: () => !!accessToken.current && !!organizationId,
        queryFn: async () => {
            const client = useFetchClient();
            const response = await client.getJSON<ApiCustomFieldDefinition[]>(`organizations/${organizationId}/event-custom-fields`);
            return response.data?.map(mapApiDefinition) ?? [];
        },
        queryKey: queryKeys.customFields(organizationId),
        staleTime: CUSTOM_FIELD_QUERY_STALE_TIME_MS
    };
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
            queryClient.invalidateQueries({
                queryKey: queryKeys.customFields(request.route.organizationId)
            });
        }
    }));
}

export function getCustomFieldsQuery(request: GetCustomFieldsRequest) {
    return createQuery<CustomFieldDefinition[], ProblemDetails>(() => createCustomFieldsQueryOptions(request));
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
            queryClient.invalidateQueries({
                queryKey: queryKeys.customFields(request.route.organizationId)
            });
        }
    }));
}

function mapApiDefinition(definition: ApiCustomFieldDefinition): CustomFieldDefinition {
    return {
        createdUtc: definition.created_utc,
        description: definition.description ?? undefined,
        displayOrder: definition.display_order,
        id: definition.id,
        indexType: parseApiIndexType(definition.index_type),
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
