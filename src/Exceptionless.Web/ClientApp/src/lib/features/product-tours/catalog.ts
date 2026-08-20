import type { ProductTourProgress } from '$features/users/models';

import { resolve } from '$app/paths';

import type { ProductTourContext, ProductTourDefinition, ProductTourListItem, ProductTourStartAction, ProductTourStep } from './types';

import { PRODUCT_TOUR_ANCHORS } from './anchors';

function getConfigureProjectStartAction(context: ProductTourContext): ProductTourStartAction {
    if (!context.organizationId) {
        return { destination: resolve('/(app)/organization/add'), type: 'navigate' };
    }

    if (context.pathname.includes('/project/add')) {
        return { type: 'launch' };
    }

    const project = context.projects.find((item) => !item.is_configured);
    if (project?.id) {
        return {
            destination: `${resolve('/(app)/project/[projectId]/configure', { projectId: project.id })}?redirect=true`,
            type: 'navigate'
        };
    }

    return {
        actionLabel: 'Create Project',
        description: 'Every accessible project is already configured. A new project uses plan capacity and will remain after the guide.',
        destination: resolve('/(app)/project/add'),
        title: 'Create another project?',
        type: 'confirm-navigation'
    };
}

function getInvestigateErrorStartAction(context: ProductTourContext): ProductTourStartAction {
    if (context.openEventType === 'error') {
        return { stepId: 'stack-summary', type: 'launch' };
    }

    const destination = `${resolve('/(app)/event')}?time=all&type=error`;
    if (context.pathname === resolve('/(app)/event')) {
        return { destination, type: 'navigate' };
    }

    return {
        actionLabel: 'Open Errors',
        description: 'This guide starts in Errors so you can choose a real report. Your current page will change.',
        destination,
        title: 'Open Errors?',
        type: 'confirm-navigation'
    };
}

export const investigateErrorSteps: readonly ProductTourStep[] = [
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventFilters,
        description: 'Errors are already selected. Narrow the list by project, status, date, version, tags, or search terms when you need a specific incident.',
        id: 'filter-errors',
        title: 'Start with the right errors'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventList,
        description: 'Choose an error row to open its detail sheet. The guide continues only after a real error report is loaded.',
        id: 'choose-error',
        showDone: false,
        title: 'Open a real error'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventStack,
        description:
            'The stack groups matching occurrences into one issue. Use its title, total events, affected users, first and last occurrence, and trend to judge scope and impact.',
        id: 'stack-summary',
        presentation: 'inline',
        title: 'Understand the grouped issue',
        waitForElement: 60000
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventStackTriage,
        description:
            'Status tracks the team workflow: open, fixed, snoozed, ignored, or discarded. Options manage critical occurrences, external promotion, references, stacking information, and deletion. These actions change shared state, so this guide will not click them.',
        id: 'stack-triage',
        presentation: 'inline',
        title: 'Triage deliberately',
        waitForElement: 60000
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventOccurrence,
        description:
            'Below the stack is this specific occurrence. Its timestamp, raw JSON, and older/newer controls help you compare what happened at one moment with neighboring events.',
        id: 'event-occurrence',
        presentation: 'inline',
        title: 'Inspect the occurrence'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventTabOverview,
        description: 'Overview summarizes the message and the most useful event fields. Field filter icons can turn evidence into a narrower Events query.',
        id: 'tab-overview',
        presentation: 'inline',
        title: 'Begin with the overview'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventTabException,
        description: 'Exception shows the error type, message, stack trace, and inner exceptions. Start here when you need the failing code path.',
        id: 'tab-exception',
        optional: true,
        presentation: 'inline',
        title: 'Follow the exception'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventTabRequest,
        description: 'Request captures the URL, HTTP method, client, headers, cookies, and other request data that may explain how the failure was reached.',
        id: 'tab-request',
        optional: true,
        presentation: 'inline',
        title: 'Reconstruct the request'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventTabEnvironment,
        description: 'Environment identifies the machine, runtime, architecture, and process context so you can spot deployment-specific failures.',
        id: 'tab-environment',
        optional: true,
        presentation: 'inline',
        title: 'Check where it happened'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventTabTrace,
        description: 'Trace Log provides the diagnostic trail captured around this occurrence. It appears only when trace data was submitted.',
        id: 'tab-trace',
        optional: true,
        presentation: 'inline',
        title: 'Read the surrounding trace'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventTabSession,
        description: 'Session Events shows nearby activity from the same session, helping you understand the actions that led to the error.',
        id: 'tab-session',
        optional: true,
        presentation: 'inline',
        title: 'Follow the user session'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventTabExtendedData,
        description:
            'Extended Data contains application-specific values that did not fit the standard fields. Treat it as supporting evidence after the core exception and request details.',
        id: 'tab-extended-data',
        optional: true,
        presentation: 'inline',
        title: 'Review custom context'
    },
    {
        anchor: PRODUCT_TOUR_ANCHORS.eventStackFilter,
        description:
            'When you are ready to compare occurrences, use Show all events to filter the Events list to this stack. The guide leaves the page unchanged so you decide when to pivot.',
        id: 'filter-stack-events',
        presentation: 'inline',
        showDone: true,
        title: 'Compare every occurrence'
    }
] as const;

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
                title: 'Help is always nearby',
                waitForElement: 5000
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
        getStartAction: getConfigureProjectStartAction,
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
        getStartAction: () => ({ destination: resolve('/(app)/event'), type: 'navigate' }),
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
        description: 'Filter to a real error, assess its stack and status, then inspect the evidence available for that occurrence.',
        getAvailability: requireErrorEvent,
        getStartAction: getInvestigateErrorStartAction,
        getSteps: () => [...investigateErrorSteps],
        id: 'investigate-error',
        keywords: ['error report', 'event details', 'exception', 'request', 'environment', 'filter', 'stack', 'status', 'triage'],
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
