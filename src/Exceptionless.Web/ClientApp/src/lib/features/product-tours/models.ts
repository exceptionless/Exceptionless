import type { AssistantAccess } from '$features/assistant/models';
import type { ViewProject } from '$features/projects/models';
import type { KeyboardShortcut } from '$features/shared/keyboard-shortcuts';
import type { ProductTourState } from '$features/users/models';
export const PRODUCT_TOUR_CHECKPOINTS = {
    'app-overview': ['navigation', 'command-search', 'saved-views', 'exie', 'help'],
    'event-investigate': ['filter-errors', 'choose-error', 'stack-summary', 'stack-triage', 'event-occurrence', 'tab-overview', 'filter-stack-events'],
    'exie-overview': ['open-exie', 'exie-context'],
    'project-configure': ['organization-name', 'project-name', 'choose-platform', 'sdk-instructions', 'wait-for-event', 'event-received'],
    'saved-view-create': ['open-view-menu', 'review-settings', 'name-view', 'private-view', 'save-view', 'view-created']
} as const;

export const PRODUCT_TOUR_LAUNCH_SOURCES = ['welcome', 'catalog', 'command-palette', 'feature-announcement', 'help-menu'] as const;

export interface ProductTourAvailability {
    available: boolean;
    reason?: string;
}
export type ProductTourCheckpoint<Name extends ProductTourName = ProductTourName> = Name extends ProductTourName
    ? {
          checkpointName: ProductTourCheckpointName<Name>;
          organizationId?: string;
          source: ProductTourLaunchSource;
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
    canResume: (checkpointName: ProductTourCheckpointName<Name>, routeId: null | string) => boolean;
    description: string;
    keywords: readonly string[];
    name: Name;
    recordName: ProductTourRecordName;
    start: (context: ProductTourContext) => ProductTourStart<Name>;
    stateKey: keyof ProductTourState;
    title: string;
}
export type ProductTourKey = 'app-welcome' | 'exie-announcement' | ProductTourName;

export type ProductTourLaunchSource = (typeof PRODUCT_TOUR_LAUNCH_SOURCES)[number];

export interface ProductTourListItem<Name extends ProductTourName = ProductTourName> extends ProductTourDefinition<Name> {
    currentAvailability: ProductTourAvailability;
    recordedAt?: null | string;
}

export type ProductTourName = keyof typeof PRODUCT_TOUR_CHECKPOINTS;

export type ProductTourRecordName =
    'app-overview' | 'app-welcome' | 'event-investigate' | 'exie-announcement' | 'exie-overview' | 'project-configure' | 'saved-view-create';

export interface ProductTourShortcut {
    label: string;
    shortcut: KeyboardShortcut;
}

export interface ProductTourStart<Name extends ProductTourName = ProductTourName> {
    checkpointName: ProductTourCheckpointName<Name>;
    route: string;
}
