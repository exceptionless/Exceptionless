import { beforeEach, describe, expect, it } from 'vitest';

import type { ProductTourCheckpoint } from './models';

import { productTourCheckpoint } from './state.svelte';

const checkpoint: ProductTourCheckpoint = {
    checkpointName: 'navigation',
    organizationId: 'organization-id',
    source: 'catalog',
    tourName: 'app-overview',
    userId: 'user-id',
    version: 1
};

describe('product tour checkpoint store', () => {
    beforeEach(() => productTourCheckpoint.clear());

    it('preserves the current checkpoint across forward and back navigation', () => {
        // Arrange
        const first = productTourCheckpoint.start('app-overview', 'navigation', 'catalog', 'user', 1);

        // Act & Assert
        const second = productTourCheckpoint.advance(first, 'command-search')!;
        expect(productTourCheckpoint.current).toBe(second);
        const back = productTourCheckpoint.advance(second, 'navigation')!;
        expect(back.checkpointName).toBe('navigation');
        const replay = productTourCheckpoint.start('app-overview', 'navigation', 'catalog', 'user', 1);
        expect(productTourCheckpoint.current).toBe(replay);
    });

    it('does not let stale work advance or clear a newer tour', () => {
        const first = productTourCheckpoint.start(
            checkpoint.tourName,
            checkpoint.checkpointName,
            checkpoint.source,
            checkpoint.userId,
            checkpoint.version,
            checkpoint.organizationId
        );
        const second = productTourCheckpoint.start(
            checkpoint.tourName,
            checkpoint.checkpointName,
            'help-menu',
            checkpoint.userId,
            checkpoint.version,
            checkpoint.organizationId
        );

        expect(productTourCheckpoint.advance(first, 'command-search')).toBeUndefined();
        expect(productTourCheckpoint.clear(first)).toBe(false);
        expect(productTourCheckpoint.current).toBe(second);
    });

    it('clears a checkpoint restored for another identity', () => {
        productTourCheckpoint.start(
            checkpoint.tourName,
            checkpoint.checkpointName,
            checkpoint.source,
            checkpoint.userId,
            checkpoint.version,
            checkpoint.organizationId
        );
        productTourCheckpoint.clear();
        sessionStorage.setItem('exceptionless.product-tour', JSON.stringify(checkpoint));

        expect(productTourCheckpoint.restore('another-user', 'organization-id')).toBeUndefined();
        expect(sessionStorage.getItem('exceptionless.product-tour')).toBeNull();
    });
});
