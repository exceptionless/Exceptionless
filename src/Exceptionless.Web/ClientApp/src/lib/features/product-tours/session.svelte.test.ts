import { beforeEach, describe, expect, it } from 'vitest';

import type { ProductTourCheckpoint } from './models';

import { clearProductTourSession, readProductTourSession, writeProductTourSession } from './session';

const checkpoint: ProductTourCheckpoint = {
    checkpointName: 'choose-error',
    organizationId: 'organization-id',
    source: 'command-palette',
    tourName: 'event-investigate',
    userId: 'user-id',
    version: 1
};

describe('product tour session', () => {
    beforeEach(() => {
        sessionStorage.clear();
    });

    it('round-trips an explicit checkpoint', () => {
        writeProductTourSession(checkpoint);
        expect(readProductTourSession()).toEqual(checkpoint);
        clearProductTourSession();
        expect(sessionStorage).toHaveLength(0);
    });

    it.each([
        '{not-json',
        JSON.stringify({ ...checkpoint, tourName: 'unknown-tour' }),
        JSON.stringify({ ...checkpoint, checkpointName: 'unknown-step' }),
        JSON.stringify({ ...checkpoint, source: 'unknown-source' }),
        JSON.stringify({ ...checkpoint, version: 0 }),
        JSON.stringify({ ...checkpoint, userId: 42 })
    ])('clears malformed or unknown stored state: %s', (value) => {
        sessionStorage.setItem('exceptionless.product-tour', value);
        expect(readProductTourSession()).toBeUndefined();
        expect(sessionStorage).toHaveLength(0);
    });

    it('drops obsolete workflow state from an existing browser session', () => {
        sessionStorage.setItem('exceptionless.product-tour', JSON.stringify({ ...checkpoint, phase: { type: 'saved-view-created', viewId: 'view-id' } }));
        expect(readProductTourSession()).toEqual(checkpoint);
    });
});
