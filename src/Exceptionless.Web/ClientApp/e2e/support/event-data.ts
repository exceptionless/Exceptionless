import type { E2EApiClient, E2EEvent } from '../fixtures/api-client';

import { createRepresentativeEvent } from './synthetic-event';

interface SeedRepresentativeEventOptions {
    message: string;
    projectId: string;
    projectToken: string;
    referenceId: string;
}

export async function seedRepresentativeEvent(
    e2eApi: E2EApiClient,
    userToken: string,
    { message, projectId, projectToken, referenceId }: SeedRepresentativeEventOptions
): Promise<E2EEvent> {
    await e2eApi.submitEvent(
        projectId,
        projectToken,
        createRepresentativeEvent({
            appUrl: e2eApi.environment.appUrl,
            message,
            referenceId,
            runId: e2eApi.environment.runId
        })
    );

    return await e2eApi.pollForEventByReference(userToken, projectId, referenceId);
}
