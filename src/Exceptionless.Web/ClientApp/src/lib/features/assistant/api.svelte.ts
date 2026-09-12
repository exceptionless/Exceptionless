import type { QueryClient } from '@tanstack/svelte-query';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createMutation, createQuery, useQueryClient } from '@tanstack/svelte-query';

import type { AssistantAccess, AssistantConversationSharingSettings } from './models';

export const queryKeys = {
    access: (organizationId: string | undefined) => [...queryKeys.type, 'access', organizationId] as const,
    conversationSharing: ['Assistant', 'conversation-sharing'] as const,
    type: ['Assistant'] as const
};

interface GetAssistantAccessRequest {
    route: {
        organizationId: string | undefined;
    };
}

export function getAssistantAccessQuery(request: GetAssistantAccessRequest) {
    return createQuery<AssistantAccess, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && !!request.route.organizationId,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<AssistantAccess>('assistant/access', {
                params: {
                    organization_id: request.route.organizationId
                },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.access(request.route.organizationId),
        staleTime: 5 * 60 * 1000
    }));
}

export function getAssistantConversationSharingQuery(request: { enabled: boolean }) {
    return createQuery<AssistantConversationSharingSettings, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current && request.enabled,
        queryFn: async ({ signal }) => {
            const response = await useFetchClient().getJSON<AssistantConversationSharingSettings>('assistant/conversation-sharing', {
                signal
            });
            if (!response.ok) {
                throw response.problem;
            }
            return response.data!;
        },
        queryKey: queryKeys.conversationSharing
    }));
}

export async function invalidateAssistantAccessQueries(queryClient: QueryClient): Promise<void> {
    await queryClient.invalidateQueries({
        queryKey: queryKeys.type
    });
}

export function putAssistantConversationSharingMutation() {
    const queryClient = useQueryClient();
    return createMutation<AssistantConversationSharingSettings, ProblemDetails, { enabled: boolean | null }>(() => ({
        mutationFn: async (request) => {
            const response = await useFetchClient().putJSON<AssistantConversationSharingSettings>('assistant/conversation-sharing', request);
            if (!response.ok) {
                throw response.problem;
            }
            return response.data!;
        },
        onMutate: async () => {
            await queryClient.cancelQueries({
                queryKey: queryKeys.conversationSharing
            });
        },
        onSuccess: (settings) => {
            queryClient.setQueryData(queryKeys.conversationSharing, settings);
        }
    }));
}
