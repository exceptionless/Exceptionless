import type { ProductTourProgress } from '$features/users/models';

import type { ProductTourContext, ProductTourDefinition, ProductTourListItem } from './types';

import { PRODUCT_TOUR_ANCHORS } from './anchors';

function requireApplicationShell(context: ProductTourContext) {
    if (context.isSetupPage || !context.organizationId) {
        return { available: false, reason: 'Finish organization setup to explore the application UI.' };
    }

    return { available: true };
}

function requireErrorEvent(context: ProductTourContext) {
    if (!context.organizationId) {
        return { available: false, reason: 'Create an organization and project first.' };
    }

    if (context.errorEventAvailability === 'loading') {
        return { available: false, reason: 'Checking for an accessible error report…' };
    }

    if (context.errorEventAvailability === 'error') {
        return { available: false, reason: 'Error reports could not be checked right now. Try again shortly.' };
    }

    if (context.errorEventAvailability === 'empty') {
        return { available: false, reason: 'Send or retain an error report before starting this guide.' };
    }

    return { available: true };
}

function requireOrganization(context: ProductTourContext) {
    return context.organizationId ? { available: true } : { available: false, reason: 'Create an organization and project first.' };
}

export const productTourCatalog: readonly ProductTourDefinition[] = [
    {
        description: 'Learn navigation, command search, saved views, Exie, and where to get help.',
        getAvailability: requireApplicationShell,
        getSteps: (context) => [
            {
                anchor: PRODUCT_TOUR_ANCHORS.appNavigation,
                description: 'Move between dashboards, saved views, and settings from one consistent navigation area.',
                id: 'navigation',
                title: 'Your workspace navigation'
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.commandSearch,
                description: 'Open this search or press / to jump to pages, projects, events, stacks, and actions.',
                id: 'command-search',
                title: 'Find anything quickly'
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.savedViewNavigation,
                description: 'Saved views capture filters, time, sorting, charts, stats, and columns for quick reuse.',
                id: 'saved-views',
                optional: true,
                title: 'Reuse configured views'
            },
            ...(context.assistantAccess?.enabled
                ? [
                      {
                          anchor: PRODUCT_TOUR_ANCHORS.exieTrigger,
                          description: context.assistantAccess.has_access
                              ? 'Exie can investigate the page or error you are viewing. You always choose whether to send a prompt.'
                              : (context.assistantAccess.message ?? 'Exie is available after upgrading your organization plan.'),
                          id: 'exie',
                          optional: true,
                          title: 'Ask Exie with context'
                      }
                  ]
                : []),
            {
                anchor: PRODUCT_TOUR_ANCHORS.helpMenu,
                description: 'Open Help for documentation, support, keyboard shortcuts, and these guided tours.',
                id: 'help',
                optional: true,
                showDone: true,
                title: 'Help is always nearby'
            }
        ],
        id: 'new-ui-overview',
        keywords: ['navigation', 'new ui', 'search', 'command', 'help', 'saved views'],
        title: 'Explore the new UI',
        version: 1
    },
    {
        description: 'Create or resume a project, connect an SDK, and wait for its first real event.',
        getAvailability: () => ({ available: true }),
        getSteps: (context) => {
            if (!context.organizationId) {
                return [
                    {
                        anchor: PRODUCT_TOUR_ANCHORS.setupOrganizationName,
                        description: 'Create the organization that will own your projects and error data.',
                        id: 'organization-name',
                        title: 'Name your organization'
                    },
                    {
                        anchor: PRODUCT_TOUR_ANCHORS.projectName,
                        description: 'Use the application or service name that will send events to Exceptionless.',
                        id: 'project-name',
                        title: 'Name your first project'
                    },
                    {
                        advanceOnClick: true,
                        anchor: PRODUCT_TOUR_ANCHORS.projectSetupSubmit,
                        description: 'Create both records, then continue to the SDK instructions.',
                        id: 'create-setup',
                        resumeStepId: 'choose-platform',
                        title: 'Continue to configuration'
                    }
                ];
            }

            if (context.pathname.includes('/project/add')) {
                return [
                    {
                        anchor: PRODUCT_TOUR_ANCHORS.projectName,
                        description: 'Use the application or service name that will send events to Exceptionless.',
                        id: 'project-name',
                        title: 'Name your project'
                    },
                    {
                        advanceOnClick: true,
                        anchor: PRODUCT_TOUR_ANCHORS.projectSetupSubmit,
                        description: 'Create the project, then continue to its SDK instructions.',
                        id: 'create-project',
                        resumeStepId: 'choose-platform',
                        title: 'Continue to configuration'
                    }
                ];
            }

            return [
                {
                    anchor: PRODUCT_TOUR_ANCHORS.projectConfigurePlatform,
                    description: 'Choose the platform that matches the application you are connecting.',
                    id: 'choose-platform',
                    title: 'Choose your SDK'
                },
                {
                    anchor: PRODUCT_TOUR_ANCHORS.projectConfigureToken,
                    description: 'The generated client token identifies this project. Keep it with your application configuration.',
                    id: 'client-token',
                    optional: true,
                    title: 'Use the project token'
                },
                {
                    anchor: PRODUCT_TOUR_ANCHORS.projectConfigureInstructions,
                    description: 'Follow these instructions in your own application. This guide stays ready while you work outside Exceptionless.',
                    id: 'sdk-instructions',
                    presentation: 'inline',
                    title: 'Connect your application'
                },
                {
                    anchor: PRODUCT_TOUR_ANCHORS.projectConfigureWaiting,
                    description: 'The guide completes only after this project sends its first real event.',
                    id: 'wait-for-event',
                    presentation: 'inline',
                    showDone: false,
                    title: 'Waiting for the first event'
                }
            ];
        },
        id: 'configure-project',
        keywords: ['add project', 'configure', 'sdk', 'api key', 'first event'],
        title: 'Configure a project',
        version: 1
    },
    {
        description: 'Save the current Events configuration as a private view that only you can see.',
        getAvailability: requireOrganization,
        getSteps: () => [
            {
                advanceOnClick: true,
                anchor: PRODUCT_TOUR_ANCHORS.savedViewTrigger,
                description: 'A saved view can capture the current filters, date range, sort, display choices, and columns.',
                id: 'open-view-menu',
                title: 'Open View settings'
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.savedViewSettings,
                description: 'Review filters, time range, sorting, chart and stat choices, and columns here. The guide will not change them for you.',
                id: 'review-settings',
                title: 'Configure what the view remembers',
                waitForElement: 5000
            },
            {
                advanceOnClick: true,
                anchor: PRODUCT_TOUR_ANCHORS.savedViewSaveAs,
                description: 'Save As creates a reusable view without changing any existing view.',
                id: 'save-as',
                title: 'Create a new view',
                waitForElement: 5000
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.savedViewName,
                description: 'Choose a meaningful name. The URL name is generated automatically.',
                id: 'name-view',
                presentation: 'inline',
                title: 'Name the view',
                waitForElement: 5000
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.savedViewPrivate,
                description: 'Private is enabled for this guide so the practice view does not affect your organization.',
                id: 'private-view',
                presentation: 'inline',
                title: 'Keep it private'
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.savedViewSubmit,
                description: 'Save when ready. Completion is recorded only after the view is successfully created and loaded.',
                id: 'save-view',
                presentation: 'inline',
                showDone: false,
                title: 'Create the saved view'
            }
        ],
        id: 'create-saved-view',
        keywords: ['saved view', 'filter', 'columns', 'private', 'dashboard'],
        title: 'Create a saved view',
        version: 1
    },
    {
        description: 'Open a real error and learn where to find its exception, request, environment, and custom data.',
        getAvailability: requireErrorEvent,
        getSteps: () => [
            {
                anchor: PRODUCT_TOUR_ANCHORS.eventList,
                description: 'Choose an error row to open its detail sheet. The guide will continue when the error report is loaded.',
                id: 'choose-error',
                showDone: false,
                title: 'Open a real error'
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.eventDetails,
                description: 'Review the summary and the available Exception, Request, Environment, trace, session, and extended-data tabs.',
                id: 'inspect-details',
                presentation: 'inline',
                showDone: true,
                title: 'Investigate the evidence',
                waitForElement: 60000
            }
        ],
        id: 'investigate-error',
        keywords: ['error report', 'event details', 'exception', 'request', 'environment'],
        title: 'Investigate an error',
        version: 1
    },
    {
        description: 'See how Exie uses the current page as context without sending a prompt.',
        getAvailability: (context) =>
            context.assistantAccess?.enabled ? { available: true } : { available: false, reason: 'Exie is not enabled by this Exceptionless installation.' },
        getSteps: (context) => [
            {
                advanceOnClick: true,
                anchor: PRODUCT_TOUR_ANCHORS.exieTrigger,
                description: context.assistantAccess?.has_access
                    ? 'Open Exie to see the page context available for your next question.'
                    : (context.assistantAccess?.message ?? 'Open Exie to review the plan requirement.'),
                id: 'open-exie',
                title: 'Open Exie'
            },
            {
                anchor: PRODUCT_TOUR_ANCHORS.exiePanel,
                description: context.assistantAccess?.has_access
                    ? 'Exie can investigate with your current organization, project, event, or stack context. Nothing is sent until you choose a prompt; submitted requests use metered provider usage.'
                    : 'This panel explains the access requirement. The guide will never start a provider request.',
                id: 'exie-context',
                showDone: true,
                title: 'You control every request',
                waitForElement: 5000
            }
        ],
        id: 'meet-exie',
        keywords: ['exie', 'assistant', 'ai', 'help', 'investigate'],
        title: 'Meet Exie',
        version: 1
    }
] as const;

export function getProductTour(id: ProductTourDefinition['id']): ProductTourDefinition {
    const definition = productTourCatalog.find((tour) => tour.id === id);
    if (!definition) {
        throw new Error(`Unknown product tour: ${id}`);
    }

    return definition;
}

export function getProductTourItems(context: ProductTourContext, progress: Record<string, ProductTourProgress> = {}): ProductTourListItem[] {
    return productTourCatalog.map((definition) => ({
        ...definition,
        availability: definition.getAvailability(context),
        progress: progress[definition.id]
    }));
}

export function getRecommendedProductTourId(context: ProductTourContext): ProductTourDefinition['id'] {
    return !context.organizationId || context.projects.some((project) => !project.is_configured) ? 'configure-project' : 'new-ui-overview';
}
