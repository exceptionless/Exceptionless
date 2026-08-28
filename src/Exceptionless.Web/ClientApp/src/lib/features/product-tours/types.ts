import type { AssistantAccess } from '$features/assistant/models';
import type { ViewProject } from '$features/projects/models';
import type { ProductTourProgress } from '$features/users/models';
import type { ProductTourLaunchSource as ProductTourLaunchSourceContract } from '$generated/api';

export const PRODUCT_TOUR_CHECKPOINTS = {
    'configure-project': ['organization-name', 'project-name', 'choose-platform', 'sdk-instructions', 'wait-for-event'],
    'create-saved-view': ['open-view-menu', 'review-settings', 'name-view', 'private-view', 'save-view', 'view-created'],
    'investigate-error': ['filter-errors', 'choose-error', 'stack-summary', 'stack-triage', 'event-occurrence', 'tab-overview', 'filter-stack-events'],
    'meet-exie': ['open-exie', 'exie-context'],
    'ui-overview': ['navigation', 'command-search', 'saved-views', 'exie', 'help']
} as const;

export interface ProductTourAvailability {
    available: boolean;
    reason?: string;
}
export type ProductTourCheckpoint<Name extends ProductTourName = ProductTourName> = Name extends ProductTourName
    ? {
          checkpointName: ProductTourCheckpointName<Name>;
          organizationId?: string;
          phase: ProductTourPhase<Name>;
          source: ProductTourLaunchSource;
          tourName: Name;
          userId: string;
          version: number;
      }
    : never;
export type ProductTourCheckpointName<Name extends ProductTourName = ProductTourName> = (typeof PRODUCT_TOUR_CHECKPOINTS)[Name][number];
export interface ProductTourContext {
    assistantAccess?: AssistantAccess;
    errorEventAvailability: 'available' | 'empty' | 'error' | 'loading';
    isSetupPage: boolean;
    organizationId?: string;
    pathname: string;
    projects: Pick<ViewProject, 'is_configured'>[];
}
export interface ProductTourDefinition<Name extends ProductTourName = ProductTourName> {
    availability: (context: ProductTourContext) => ProductTourAvailability;
    description: string;
    initialCheckpoint: ProductTourCheckpointName<Name>;
    keywords: readonly string[];
    name: Name;
    startingRoute: (context: ProductTourContext) => string;
    title: string;
}

export type ProductTourKey = 'exie-announcement' | 'welcome' | ProductTourName;

export type ProductTourLaunchSource = `${ProductTourLaunchSourceContract}`;

export interface ProductTourListItem<Name extends ProductTourName = ProductTourName> extends ProductTourDefinition<Name> {
    currentAvailability: ProductTourAvailability;
    progress?: ProductTourProgress;
    version: number;
}

export type ProductTourName = keyof typeof PRODUCT_TOUR_CHECKPOINTS;

export type ProductTourPhase<Name extends ProductTourName = ProductTourName> =
    (Name extends 'create-saved-view' ? { type: 'saved-view-created' | 'saved-view-loaded'; viewId: string } : never) | { type: 'active' };
