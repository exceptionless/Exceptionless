import { resolve } from '$app/paths';

import type { ViewProject } from '#features/projects/models.ts';

/** The next onboarding step for an organization: configure its first quiet project, or add one. */
export function getContinueSetupHref(projects: readonly Pick<ViewProject, 'id' | 'is_configured'>[]): string {
    const project = projects.find(isProjectAwaitingFirstEvent);
    return project ? resolve('/(app)/project/[projectId]/configure', { projectId: project.id }) : resolve('/(app)/project/add');
}

/** A project that has never received an event. Older projects without the flag count as configured. */
export function isProjectAwaitingFirstEvent(project: Pick<ViewProject, 'is_configured'>): boolean {
    return project.is_configured === false;
}
