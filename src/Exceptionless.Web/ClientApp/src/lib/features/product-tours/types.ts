import type { AssistantAccess } from '$features/assistant/models';
import type { ViewProject } from '$features/projects/models';
import type { ProductTourProgress } from '$features/users/models';

export interface ProductTourAvailability {
    available: boolean;
    reason?: string;
}
export interface ProductTourContext {
    assistantAccess?: AssistantAccess;
    errorEventAvailability: ProductTourErrorEventAvailability;
    isSetupPage: boolean;
    openEventType?: string;
    organizationId?: string;
    pathname: string;
    projects: ViewProject[];
}
export interface ProductTourDefinition {
    description: string;
    getAvailability: (context: ProductTourContext) => ProductTourAvailability;
    getStartAction?: (context: ProductTourContext) => ProductTourStartAction;
    getSteps: (context: ProductTourContext) => ProductTourStep[];
    id: ProductTourId;
    keywords: readonly string[];
    title: string;
    version: number;
}
export type ProductTourErrorEventAvailability = 'available' | 'empty' | 'error' | 'loading';
export type ProductTourId = 'configure-project' | 'create-saved-view' | 'investigate-error' | 'meet-exie' | 'new-ui-overview';
export type ProductTourKey = 'exie-announcement' | 'welcome' | ProductTourId;

export type ProductTourLaunchSource = 'automatic' | 'catalog' | 'command-palette' | 'feature-announcement' | 'help-menu';

export interface ProductTourListItem extends ProductTourDefinition {
    availability: ProductTourAvailability;
    progress?: ProductTourProgress;
}

export type ProductTourPresentation = 'inline' | 'spotlight';

export type ProductTourStartAction =
    | { actionLabel: string; description: string; destination: string; title: string; type: 'confirm-navigation' }
    | { destination: string; type: 'navigate' }
    | { stepId?: string; type: 'launch' };

export interface ProductTourStep {
    advanceOnClick?: boolean;
    anchor?: string;
    description: string;
    id: string;
    optional?: boolean;
    presentation?: ProductTourPresentation;
    resumeStepId?: string;
    showDone?: boolean;
    title: string;
    waitForElement?: number;
}
