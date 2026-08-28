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
        initialCheckpoint: 'navigation',
        keywords: ['navigation', 'ui', 'search', 'command', 'help', 'saved views'],
        name: 'ui-overview',
        startingRoute: () => resolve('/'),
        title: 'Explore Exceptionless'
    },
    {
        availability: () => ({ available: true }),
        description: 'Create or resume a project, connect an SDK, and wait for its first real event.',
        initialCheckpoint: 'project-name',
        keywords: ['add project', 'configure', 'sdk', 'api key', 'first event'],
        name: 'configure-project',
        startingRoute: (context) => (context.organizationId ? resolve('/(app)/project/add') : resolve('/(app)/organization/add')),
        title: 'Configure a project'
    },
    {
        availability: requireOrganization,
        description: 'Save the current Events configuration as a private view that only you can see.',
        initialCheckpoint: 'open-view-menu',
        keywords: ['saved view', 'filter', 'columns', 'private', 'dashboard'],
        name: 'create-saved-view',
        startingRoute: () => resolve('/(app)/event'),
        title: 'Create a saved view'
    },
    {
        availability: requireError,
        description: 'Open a real error, assess its stack and status, then inspect the occurrence.',
        initialCheckpoint: 'filter-errors',
        keywords: ['error report', 'event details', 'exception', 'filter', 'stack', 'triage'],
        name: 'investigate-error',
        startingRoute: () => `${resolve('/(app)/event')}?time=all&type=error`,
        title: 'Investigate an error'
    },
    {
        availability: (context) =>
            context.assistantAccess?.enabled ? { available: true } : { available: false, reason: 'Exie is not enabled by this Exceptionless installation.' },
        description: 'See how Exie uses the current page as context without sending a prompt.',
        initialCheckpoint: 'open-exie',
        keywords: ['exie', 'assistant', 'ai', 'help', 'investigate'],
        name: 'meet-exie',
        startingRoute: () => resolve('/'),
        title: 'Meet Exie'
    }
] as const;

export function getProductTourItems(
    context: ProductTourContext,
    versions: Record<string, number>,
    progress: Record<string, ProductTourProgress> = {}
): ProductTourListItem[] {
    return productTourCatalog.map((definition) => {
        const version = versions[definition.name] ?? 0;
        return {
            ...definition,
            currentAvailability:
                version > 0 ? definition.availability(context) : { available: false, reason: 'This guided tour is not supported by the server.' },
            progress: progress[definition.name],
            version
        };
    });
}

export function getRecommendedProductTourName(context: ProductTourContext): ProductTourName {
    return !context.organizationId || context.projects.some((project) => !project.is_configured) ? 'configure-project' : 'ui-overview';
}
