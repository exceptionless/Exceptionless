import type { RouteId } from '$app/types';
import type { AssistantAccess } from '$features/assistant/models';
import type { ViewProject } from '$features/projects/models';
export const PRODUCT_TOUR_CHECKPOINTS = {
    'app-overview': ['navigation', 'events', 'filters', 'saved-views', 'exie', 'command-search'],
    'event-investigate': ['choose-error', 'stack-summary', 'tab-overview', 'filter-stack-events'],
    'exie-overview': ['open-exie', 'exie-context'],
    'project-configure': ['organization-name', 'project-name', 'choose-platform', 'sdk-instructions'],
    'saved-view-create': ['open-view-menu', 'review-settings', 'name-view']
} as const;

export interface ProductTourAvailability {
    available: boolean;
    reason?: string;
}
export type ProductTourCheckpoint<Name extends ProductTourName = ProductTourName> = Name extends ProductTourName
    ? {
          checkpointName: ProductTourCheckpointName<Name>;
          organizationId?: string;
          tourName: Name;
          userId: string;
      }
    : never;
export type ProductTourCheckpointName<Name extends ProductTourName = ProductTourName> = (typeof PRODUCT_TOUR_CHECKPOINTS)[Name][number];
export interface ProductTourContext {
    assistantAccess?: AssistantAccess;
    errorEventAvailability: 'available' | 'empty' | 'error' | 'loading';
    isProjectConfigurePage: boolean;
    isSetupPage: boolean;
    organizationId?: string;
    pathname: string;
    projects?: Pick<ViewProject, 'id' | 'is_configured'>[];
    search?: string;
}
export interface ProductTourDefinition<Name extends ProductTourName = ProductTourName> {
    availability: (context: ProductTourContext) => ProductTourAvailability;
    canResume: (checkpointName: ProductTourCheckpointName<Name>, routeId: null | RouteId) => boolean;
    description: string;
    keywords: readonly string[];
    name: Name;
    start: (context: ProductTourContext) => ProductTourStart<Name>;
    stateKey: string;
    title: string;
}
export type ProductTourKey = 'app-welcome' | 'exie-announcement' | ProductTourName;

export interface ProductTourListItem<Name extends ProductTourName = ProductTourName> extends ProductTourDefinition<Name> {
    currentAvailability: ProductTourAvailability;
    recordedAt?: null | string;
}

export type ProductTourName = keyof typeof PRODUCT_TOUR_CHECKPOINTS;

export interface ProductTourStart<Name extends ProductTourName = ProductTourName> {
    checkpointName: ProductTourCheckpointName<Name>;
    route: string;
}
