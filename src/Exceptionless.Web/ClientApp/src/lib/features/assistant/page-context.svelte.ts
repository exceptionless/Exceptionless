import type { PersistentEvent } from '#features/events/models/index.ts';
import type { Stack } from '#features/stacks/models.ts';

export interface AssistantResourceContext {
    eventId?: string;
    projectId?: string;
    stackId?: string;
    /** A readable name for the resource, shown as Exie's context in the composer. */
    title?: string;
}

class AssistantPageContext {
    private overlayOwner?: symbol;
    private overlayResource = $state<AssistantResourceContext>();
    private pageResource = $state<AssistantResourceContext>();

    public clearOverlay(owner: symbol): void {
        if (this.overlayOwner === owner) {
            this.overlayOwner = undefined;
            this.overlayResource = undefined;
        }
    }

    public getContext(eventId?: string, stackId?: string): AssistantResourceContext | undefined {
        if (this.overlayResource) {
            return this.overlayResource;
        }

        if (eventId && this.pageResource?.eventId === eventId) {
            return this.pageResource;
        }

        return stackId && this.pageResource?.stackId === stackId ? this.pageResource : undefined;
    }

    public setOverlay(owner: symbol, resource: AssistantResourceContext): void {
        this.overlayOwner = owner;
        this.overlayResource = resource;
    }

    public setOverlayEvent(owner: symbol, event: PersistentEvent): void {
        this.setOverlay(owner, {
            eventId: event.id,
            projectId: event.project_id,
            stackId: event.stack_id,
            title: getEventTitle(event)
        });
    }

    public setOverlayStack(owner: symbol, stack: Stack): void {
        this.setOverlay(owner, {
            projectId: stack.project_id,
            stackId: stack.id,
            title: stack.title || undefined
        });
    }

    public setPageEvent(event: PersistentEvent): void {
        this.pageResource = {
            eventId: event.id,
            projectId: event.project_id,
            stackId: event.stack_id,
            title: getEventTitle(event)
        };
    }

    public setPageStack(stack: Stack): void {
        this.pageResource = {
            projectId: stack.project_id,
            stackId: stack.id,
            title: stack.title || undefined
        };
    }
}

export const assistantPageContext = new AssistantPageContext();

function getEventTitle(event: PersistentEvent): string | undefined {
    return event.message?.trim() || event.type || undefined;
}
