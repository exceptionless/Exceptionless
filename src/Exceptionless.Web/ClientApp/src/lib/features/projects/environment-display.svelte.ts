import { getProjectQuery } from '#features/projects/api.svelte.ts';
import { getEnvironmentDisplayName } from '#features/projects/environments.ts';

interface Request {
    /** The environment as stored on the event. */
    environment: () => null | string | undefined;
    projectId: () => string | undefined;
}

/**
 * Resolves how an event's environment is shown. Listed environments always show in their listed casing; anything else shows the
 * way the event reported it. The project is only loaded when there is an environment to show.
 */
export function useEnvironmentDisplayName(request: Request) {
    const projectQuery = getProjectQuery({
        route: {
            get id() {
                return request.environment() ? request.projectId() : undefined;
            }
        }
    });

    return {
        get name(): string | undefined {
            const environment = request.environment();
            return environment ? getEnvironmentDisplayName(projectQuery.data?.environments, environment) : undefined;
        }
    };
}
