import type { AssistantAccess } from '$features/assistant/models';
import type { ViewProject } from '$features/projects/models';
import type { ProductTourProgress } from '$features/users/models';

export interface ProductTourAvailability {
    available: boolean;
    reason?: string;
}
export interface ProductTourContext {
    assistantAccess?: AssistantAccess;
    isSetupPage: boolean;
    organizationId?: string;
    pathname: string;
    projects: ViewProject[];
}
export interface ProductTourDefinition {
    description: string;
    getAvailability: (context: ProductTourContext) => ProductTourAvailability;
    getSteps: (context: ProductTourContext) => ProductTourStep[];
    id: ProductTourId;
    keywords: readonly string[];
    title: string;
    version: number;
}
export type ProductTourId = 'configure-project' | 'create-saved-view' | 'investigate-error' | 'meet-exie' | 'new-ui-overview';

export type ProductTourKey = 'welcome' | ProductTourId;

export type ProductTourLaunchSource = 'automatic' | 'catalog' | 'command-palette' | 'help-menu';

export interface ProductTourListItem extends ProductTourDefinition {
    availability: ProductTourAvailability;
    progress?: ProductTourProgress;
}

export type ProductTourPresentation = 'inline' | 'spotlight';

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
