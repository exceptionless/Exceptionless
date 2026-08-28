import { beforeEach, describe, expect, it } from 'vitest';

import type { ProductTourCheckpoint } from './types';

import { clearProductTourSession, readProductTourSession, writeProductTourSession } from './session';

const checkpoint: ProductTourCheckpoint = {
    checkpointName: 'choose-error',
    organizationId: 'organization-id',
    phase: { type: 'active' },
    source: 'command-palette',
    tourName: 'investigate-error',
    userId: 'user-id'
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
        JSON.stringify({ ...checkpoint, phase: { type: 'saved-view-created' } }),
        JSON.stringify({ ...checkpoint, phase: { type: 'saved-view-created', viewId: 'view-id' } }),
        JSON.stringify({ ...checkpoint, phase: { type: 'saved-view-loaded', viewId: 'view-id' } }),
        JSON.stringify({ ...checkpoint, userId: 42 })
    ])('clears malformed or unknown stored state: %s', (value) => {
        sessionStorage.setItem('exceptionless.product-tour', value);
        expect(readProductTourSession()).toBeUndefined();
        expect(sessionStorage).toHaveLength(0);
    });
});
