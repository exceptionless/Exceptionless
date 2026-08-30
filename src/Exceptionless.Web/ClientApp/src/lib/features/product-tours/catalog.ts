import type { ProductTourProgress } from '$features/users/models';

import { resolve } from '$app/paths';

import type { ProductTourContext, ProductTourDefinition, ProductTourListItem, ProductTourName } from './types';

function requireApplicationShell(context: ProductTourContext) {
    return context.isSetupPage || !context.organizationId
        ? { available: false, reason: 'Finish organization setup to explore Exceptionless.' }
        : { available: true };
}

function requireError(context: ProductTourContext) {
    if (!context.organizationId) return { available: false, reason: 'Create an organization and project first.' };
    if (context.errorEventAvailability === 'loading') return { available: false, reason: 'Checking for an accessible error report…' };
    if (context.errorEventAvailability === 'error') return { available: false, reason: 'Error reports could not be checked. Try again shortly.' };
    if (context.errorEventAvailability === 'empty') return { available: false, reason: 'Send an error report before starting this guide.' };
    return { available: true };
}

function requireOrganization(context: ProductTourContext) {
    return context.organizationId ? { available: true } : { available: false, reason: 'Create an organization and project first.' };
}

export const productTourCatalog: readonly ProductTourDefinition[] = [
    {
        availability: requireApplicationShell,
        description: 'Learn navigation, command search, saved views, Exie, and where to get help.',
        keywords: ['navigation', 'ui', 'search', 'command', 'help', 'saved views'],
        name: 'app-overview',
        start: () => ({ checkpointName: 'navigation', route: resolve('/') }),
        title: 'Explore Exceptionless',
        version: 1
    },
    {
        availability: () => ({ available: true }),
        description: 'Create or resume a project, connect an SDK, and wait for its first real event.',
        keywords: ['add project', 'configure', 'sdk', 'api key', 'first event'],
        name: 'project-configure',
        start: (context) => {
            if (!context.organizationId) {
                return { checkpointName: 'organization-name', route: resolve('/(app)/organization/add') };
            }

            const unconfiguredProject = context.projects.find((project) => !project.is_configured);
            if (unconfiguredProject?.id) {
                return {
                    checkpointName: 'choose-platform',
                    route: `${resolve('/(app)/project/[projectId]/configure', { projectId: unconfiguredProject.id })}?redirect=true`
                };
            }

            return { checkpointName: 'project-name', route: resolve('/(app)/project/add') };
        },
        title: 'Configure a project',
        version: 1
    },
    {
        availability: requireOrganization,
        description: 'Save the current Events configuration as a private view that only you can see.',
        keywords: ['saved view', 'filter', 'columns', 'private', 'dashboard'],
        name: 'saved-view-create',
        start: () => ({ checkpointName: 'open-view-menu', route: resolve('/(app)/event') }),
        title: 'Create a saved view',
        version: 1
    },
    {
        availability: requireError,
        description: 'Open a real error, assess its stack and status, then inspect the occurrence.',
        keywords: ['error report', 'event details', 'exception', 'filter', 'stack', 'triage'],
        name: 'event-investigate',
        start: () => ({ checkpointName: 'filter-errors', route: `${resolve('/(app)/event')}?time=all&type=error` }),
        title: 'Investigate an error',
        version: 1
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
        description: 'See how Exie uses the current page as context without sending a prompt.',
        keywords: ['exie', 'assistant', 'ai', 'help', 'investigate'],
        name: 'exie-overview',
        start: () => ({ checkpointName: 'open-exie', route: resolve('/') }),
        title: 'Meet Exie',
        version: 1
    }
] as const;

export function getProductTourItems(context: ProductTourContext, progress: Record<string, ProductTourProgress> = {}): ProductTourListItem[] {
    return productTourCatalog.map((definition) => {
        return {
            ...definition,
            currentAvailability: definition.availability(context),
            progress: progress[definition.name]
        };
    });
}

export function getRecommendedProductTourName(context: ProductTourContext): ProductTourName {
    return !context.organizationId || context.projects.length === 0 || context.projects.some((project) => !project.is_configured)
        ? 'project-configure'
        : 'app-overview';
}
