import type { QueryClient } from '@tanstack/svelte-query';

import { accessToken } from '$features/auth/index.svelte';
import { type ProblemDetails, useFetchClient } from '@foundatiofx/fetchclient';
import { createQuery } from '@tanstack/svelte-query';

import type { AssistantAccess } from './models';

export const queryKeys = {
    access: (organizationId: string | undefined) => [...queryKeys.type, 'access', organizationId] as const,
    type: ['Assistant'] as const
};

interface GetAssistantAccessRequest {
    route: {
        organizationId: string | undefined;
    };
}

export function getAssistantAccessQuery(request: GetAssistantAccessRequest) {
    return createQuery<AssistantAccess, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<AssistantAccess>('assistant/access', {
                params: { organization_id: request.route.organizationId },
                signal
            });

            return response.data!;
        },
        queryKey: queryKeys.access(request.route.organizationId),
        staleTime: 5 * 60 * 1000
    }));
}

export async function invalidateAssistantAccessQueries(queryClient: QueryClient): Promise<void> {
    await queryClient.invalidateQueries({ queryKey: queryKeys.type });
}
