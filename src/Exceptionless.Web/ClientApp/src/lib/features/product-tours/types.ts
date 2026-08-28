import type { AssistantAccess } from '$features/assistant/models';
import type { ViewProject } from '$features/projects/models';
import type { ProductTourProgress } from '$features/users/models';

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
export interface ProductTourCheckpoint {
    checkpointName: ProductTourCheckpointName;
    organizationId?: string;
    phase: ProductTourPhase;
    source: ProductTourLaunchSource;
    tourName: ProductTourName;
    userId: string;
}
export type ProductTourCheckpointName = (typeof PRODUCT_TOUR_CHECKPOINTS)[ProductTourName][number];
export interface ProductTourContext {
    assistantAccess?: AssistantAccess;
    errorEventAvailability: 'available' | 'empty' | 'error' | 'loading';
    isSetupPage: boolean;
    organizationId?: string;
    pathname: string;
    projects: Pick<ViewProject, 'is_configured'>[];
}
export interface ProductTourDefinition {
    availability: (context: ProductTourContext) => ProductTourAvailability;
    description: string;
    initialCheckpoint: ProductTourCheckpointName;
    keywords: readonly string[];
    name: ProductTourName;
    startingRoute: (context: ProductTourContext) => string;
    title: string;
    version: number;
}

export type ProductTourKey = 'exie-announcement' | 'welcome' | ProductTourName;

export type ProductTourLaunchSource = 'automatic' | 'catalog' | 'command-palette' | 'feature-announcement' | 'help-menu';

export interface ProductTourListItem extends ProductTourDefinition {
    currentAvailability: ProductTourAvailability;
    progress?: ProductTourProgress;
}

export type ProductTourName = keyof typeof PRODUCT_TOUR_CHECKPOINTS;

export type ProductTourPhase = { type: 'active' } | { type: 'saved-view-created' | 'saved-view-loaded'; viewId: string };
