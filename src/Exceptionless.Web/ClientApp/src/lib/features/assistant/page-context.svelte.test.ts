import type { PersistentEvent } from '$features/events/models';

import { describe, expect, it } from 'vitest';

import { assistantPageContext } from './page-context.svelte';

describe('assistantPageContext', () => {
    it('prioritizes an owned overlay and restores the matching page context when it closes', () => {
        const pageEvent = {
            id: 'page-event',
            project_id: 'page-project',
            stack_id: 'page-stack'
        } as PersistentEvent;
        const overlayEvent = {
            id: 'overlay-event',
            project_id: 'overlay-project',
            stack_id: 'overlay-stack'
        } as PersistentEvent;
        const overlayOwner = Symbol('overlay');

        assistantPageContext.setPageEvent(pageEvent);
        expect(assistantPageContext.getContext(pageEvent.id, pageEvent.stack_id)).toEqual({
            eventId: 'page-event',
            projectId: 'page-project',
            stackId: 'page-stack'
        });

        assistantPageContext.setOverlay(overlayOwner, { stackId: overlayEvent.stack_id });
        expect(assistantPageContext.getContext(pageEvent.id, pageEvent.stack_id)).toEqual({ stackId: 'overlay-stack' });

        assistantPageContext.setOverlayEvent(overlayOwner, overlayEvent);
        expect(assistantPageContext.getContext(pageEvent.id, pageEvent.stack_id)).toEqual({
            eventId: 'overlay-event',
            projectId: 'overlay-project',
            stackId: 'overlay-stack'
        });

        assistantPageContext.clearOverlay(Symbol('different-overlay'));
        expect(assistantPageContext.getContext(pageEvent.id, pageEvent.stack_id)?.eventId).toBe('overlay-event');

        assistantPageContext.clearOverlay(overlayOwner);
        expect(assistantPageContext.getContext(pageEvent.id, pageEvent.stack_id)?.eventId).toBe('page-event');
    });
});
