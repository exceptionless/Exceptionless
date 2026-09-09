import type { ProductTourState } from '$features/users/models';

import { resolve } from '$app/paths';

import type { ProductTourContext, ProductTourDefinition, ProductTourListItem, ProductTourName } from './models';

function requireApplicationShell(context: ProductTourContext) {
    return context.isSetupPage || !context.organizationId
        ? { available: false, reason: 'Finish organization setup to explore Exceptionless.' }
        : { available: true };
}

function requireError(context: ProductTourContext) {
    if (!context.organizationId) {
        return { available: false, reason: 'Create an organization and project first.' };
    }
    if (context.errorEventAvailability === 'loading') {
        return { available: false, reason: 'Checking for an accessible error report…' };
    }
    if (context.errorEventAvailability === 'error') {
        return { available: false, reason: 'Error reports could not be checked. Try again shortly.' };
    }
    if (context.errorEventAvailability === 'empty') {
        return { available: false, reason: 'Send an error report before starting this guide.' };
    }
    return { available: true };
}

function requireOrganization(context: ProductTourContext) {
    return context.organizationId ? { available: true } : { available: false, reason: 'Create an organization and project first.' };
}

export const productTourCatalog: readonly ProductTourDefinition[] = [
    {
        availability: requireApplicationShell,
        canResume: () => true,
        description: 'Navigate stacks and events, use the command palette, and reopen saved views.',
        keywords: ['navigation', 'ui', 'search', 'command palette', 'help', 'saved views', 'stacks', 'occurrences'],
        name: 'app-overview',
        start: () => ({ checkpointName: 'navigation', route: resolve('/') }),
        stateKey: 'app_overview',
        title: 'Explore Exceptionless'
    },
    {
        availability: (context) =>
            context.isProjectConfigurePage || !context.organizationId || context.projects
                ? { available: true }
                : { available: false, reason: 'Projects could not be loaded. Try again shortly.' },
        canResume: (checkpoint, routeId) => {
            if (checkpoint === 'organization-name') {
                return routeId === '/(app)/organization/add';
            }
            if (checkpoint === 'project-name') {
                return routeId === '/(app)/organization/add' || routeId === '/(app)/project/add';
            }
            return routeId === '/(app)/project/[projectId]/configure';
        },
        description: 'Continue an unfinished project, or create one and send its first event.',
        keywords: ['add project', 'configure', 'sdk', 'api key', 'first event'],
        name: 'project-configure',
        start: (context) => {
            if (context.isProjectConfigurePage) {
                const search = new URLSearchParams(context.search);
                search.set('redirect', 'true');
                return { checkpointName: 'choose-platform', route: `${context.pathname}?${search}` };
            }

            if (!context.organizationId) {
                return { checkpointName: 'organization-name', route: resolve('/(app)/organization/add') };
            }

            const unconfiguredProject = context.projects?.find((project) => !project.is_configured);
            if (unconfiguredProject?.id) {
                return {
                    checkpointName: 'choose-platform',
                    route: `${resolve('/(app)/project/[projectId]/configure', { projectId: unconfiguredProject.id })}?redirect=true`
                };
            }

            return { checkpointName: 'project-name', route: resolve('/(app)/project/add') };
        },
        stateKey: 'project_configure',
        title: 'Configure a project'
    },
    {
        availability: requireOrganization,
        canResume: (checkpoint, routeId) => routeId === '/(app)/event' && checkpoint === 'open-view-menu',
        description: 'Save your event filters and layout in a view only you can see.',
        keywords: ['saved view', 'filter', 'columns', 'private', 'dashboard'],
        name: 'saved-view-create',
        start: () => ({ checkpointName: 'open-view-menu', route: resolve('/(app)/event') }),
        stateKey: 'saved_view_create',
        title: 'Create a saved view'
    },
    {
        availability: requireError,
        canResume: (checkpoint, routeId) => routeId === '/(app)/event' && (checkpoint === 'filter-errors' || checkpoint === 'choose-error'),
        description: 'Understand stacks, review status, and inspect individual event occurrences.',
        keywords: ['error report', 'event details', 'occurrences', 'exception', 'filter', 'stack', 'triage'],
        name: 'event-investigate',
        start: () => ({ checkpointName: 'filter-errors', route: `${resolve('/(app)/event')}?time=all&type=error` }),
        stateKey: 'event_investigate',
        title: 'Investigate an error'
    },
    {
        availability: (context) => {
            if (!context.assistantAccess?.enabled) {
                return { available: false, reason: 'Exie is not enabled by this Exceptionless installation.' };
            }

            return context.assistantAccess.has_access
                ? { available: true }
                : { available: false, reason: context.assistantAccess.message ?? 'Exie requires access.' };
        },
        canResume: (checkpoint) => checkpoint === 'open-exie',
        description: 'Explore the AI assistant. This guide does not send an AI request.',
        keywords: ['exie', 'assistant', 'ai', 'help', 'investigate'],
        name: 'exie-overview',
        start: () => ({ checkpointName: 'open-exie', route: resolve('/') }),
        stateKey: 'exie_overview',
        title: 'Meet Exie'
    }
] as const;

export function getProductTourItems(context: ProductTourContext, state: ProductTourState = {}): ProductTourListItem[] {
    return productTourCatalog.map((definition) => {
        return {
            ...definition,
            currentAvailability: definition.availability(context),
            recordedAt: state[definition.stateKey]
        };
    });
}

export function getRecommendedProductTourName(context: ProductTourContext): ProductTourName {
    return !context.organizationId || context.projects?.length === 0 || context.projects?.some((project) => !project.is_configured)
        ? 'project-configure'
        : 'app-overview';
}
