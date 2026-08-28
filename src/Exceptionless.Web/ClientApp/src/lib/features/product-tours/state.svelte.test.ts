import { beforeEach, describe, expect, it } from 'vitest';

import type { ProductTourCheckpoint } from './types';

import { productTourCheckpoint } from './state.svelte';

const checkpoint: ProductTourCheckpoint = {
    checkpointName: 'navigation',
    organizationId: 'organization-id',
    phase: { type: 'active' },
    source: 'catalog',
    tourName: 'ui-overview',
    userId: 'user-id'
};

describe('product tour checkpoint store', () => {
    beforeEach(() => productTourCheckpoint.clear());

    it('does not let stale work advance or clear a newer tour', () => {
        const first = productTourCheckpoint.start(checkpoint);
        const second = productTourCheckpoint.start({ ...checkpoint, source: 'help-menu' });

        expect(productTourCheckpoint.advance(first, 'command-search')).toBeUndefined();
        expect(productTourCheckpoint.clear(first)).toBe(false);
        expect(productTourCheckpoint.current).toBe(second);
    });

    it('clears a checkpoint restored for another identity', () => {
        productTourCheckpoint.start(checkpoint);
        productTourCheckpoint.clear();
        sessionStorage.setItem('exceptionless.product-tour', JSON.stringify(checkpoint));

        expect(productTourCheckpoint.restore('another-user', 'organization-id')).toBeUndefined();
        expect(sessionStorage.getItem('exceptionless.product-tour')).toBeNull();
    });
});
