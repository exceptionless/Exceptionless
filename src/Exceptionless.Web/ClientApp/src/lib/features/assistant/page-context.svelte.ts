import type { PersistentEvent } from '$features/events/models';
import type { Stack } from '$features/stacks/models';

export interface AssistantResourceContext {
    eventId?: string;
    projectId?: string;
    stackId?: string;
}

class AssistantPageContext {
    private overlayOwner?: symbol;
    private overlayResource = $state<AssistantResourceContext>();
    private pageResource = $state<AssistantResourceContext>();

    clearOverlay(owner: symbol): void {
        if (this.overlayOwner === owner) {
            this.overlayOwner = undefined;
            this.overlayResource = undefined;
        }
    }

    getContext(eventId?: string, stackId?: string): AssistantResourceContext | undefined {
        if (this.overlayResource) {
            return this.overlayResource;
        }

        if (eventId && this.pageResource?.eventId === eventId) {
            return this.pageResource;
        }

        return stackId && this.pageResource?.stackId === stackId ? this.pageResource : undefined;
    }

    setOverlay(owner: symbol, resource: AssistantResourceContext): void {
        this.overlayOwner = owner;
        this.overlayResource = resource;
    }

    setOverlayEvent(owner: symbol, event: PersistentEvent): void {
        this.setOverlay(owner, {
            eventId: event.id,
            projectId: event.project_id,
            stackId: event.stack_id
        });
    }

    setOverlayStack(owner: symbol, stack: Stack): void {
        this.setOverlay(owner, {
            projectId: stack.project_id,
            stackId: stack.id
        });
    }

    setPageEvent(event: PersistentEvent): void {
        this.pageResource = {
            eventId: event.id,
            projectId: event.project_id,
            stackId: event.stack_id
        };
    }

    setPageStack(stack: Stack): void {
        this.pageResource = {
            projectId: stack.project_id,
            stackId: stack.id
        };
    }
}

export const assistantPageContext = new AssistantPageContext();
