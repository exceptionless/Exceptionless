import { useEventListener } from 'runed';
import { SvelteSet } from 'svelte/reactivity';

import { getOrganizationCountQuery } from '#features/events/api.svelte.ts';
import { getOrganizationProjectsQuery } from '#features/projects/api.svelte.ts';
import { ChangeType, type WebSocketMessageValue } from '#features/websockets/models.ts';

import { getContinueSetupHref, isProjectAwaitingFirstEvent } from './project-status';

/**
 * Tracks whether the current organization has received any events, combining the project
 * flags with live event notifications so empty states clear as soon as an event arrives.
 */
export function useOrganizationOnboarding(getOrganizationId: () => string | undefined) {
    const projectsQuery = getOrganizationProjectsQuery({
        route: {
            get organizationId() {
                return getOrganizationId();
            }
        }
    });

    const projectsWithLiveEvents = new SvelteSet<string>();
    const projects = $derived((projectsQuery.data?.data ?? []).filter((project) => project.organization_id === getOrganizationId()));
    const projectsReportEvents = $derived(projects.some((project) => !isProjectAwaitingFirstEvent(project) || projectsWithLiveEvents.has(project.id)));

    // A project is flagged as configured by a background job after its first event, so confirm
    // "no events yet" against the organization's event count before showing first-run states.
    const eventCountQuery = getOrganizationCountQuery({
        enabled: () => projectsQuery.isSuccess && !projectsReportEvents,
        route: {
            get organizationId() {
                return getOrganizationId();
            }
        }
    });
    const hasReceivedEvents = $derived(projectsReportEvents || (eventCountQuery.data?.total ?? 0) > 0);

    useEventListener(document, 'PersistentEventChanged', (event) => {
        const message = (event as CustomEvent<WebSocketMessageValue<'PersistentEventChanged'>>).detail;
        if (message.change_type !== ChangeType.Removed && message.organization_id === getOrganizationId() && message.project_id) {
            projectsWithLiveEvents.add(message.project_id);
        }
    });

    return {
        get continueSetupHref() {
            return getContinueSetupHref(projects.filter((project) => !projectsWithLiveEvents.has(project.id)));
        },
        get firstProject() {
            return projects.find(isProjectAwaitingFirstEvent) ?? projects[0];
        },
        get hasProject() {
            return projects.length > 0;
        },
        get hasReceivedEvents() {
            return hasReceivedEvents;
        },
        /** True once projects have loaded and none has received an event. */
        get isAwaitingFirstEvent() {
            return projectsQuery.isSuccess && !projectsReportEvents && eventCountQuery.isSuccess && eventCountQuery.data?.total === 0;
        },
        get isReady() {
            return projectsQuery.isSuccess;
        },
        get projects() {
            return projects;
        }
    };
}
