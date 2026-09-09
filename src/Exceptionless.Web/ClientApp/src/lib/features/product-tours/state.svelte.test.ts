import { flushSync } from 'svelte';
import { beforeEach, describe, expect, it } from 'vitest';

import type { ProductTourCheckpoint } from './models';

import { productTourCheckpoint } from './state.svelte';

const checkpoint: ProductTourCheckpoint = {
    checkpointName: 'navigation',
    organizationId: 'organization-id',
    tourName: 'app-overview',
    userId: 'user-id'
};

describe('product tour checkpoint store', () => {
    beforeEach(() => productTourCheckpoint.clear());

    it('preserves the current checkpoint across forward and back navigation', () => {
        // Arrange
        const first = productTourCheckpoint.start('app-overview', 'navigation', 'user');

        // Act & Assert
        const second = productTourCheckpoint.advance(first, 'command-search')!;
        expect(productTourCheckpoint.current).toBe(second);
        const back = productTourCheckpoint.advance(second, 'navigation')!;
        expect(back.checkpointName).toBe('navigation');
        const replay = productTourCheckpoint.start('app-overview', 'navigation', 'user');
        expect(productTourCheckpoint.current).toBe(replay);
    });

    it('does not retrigger an effect that clears an empty store', () => {
        let runs = 0;
        const dispose = $effect.root(() => {
            $effect(() => {
                if (!productTourCheckpoint.current) {
                    productTourCheckpoint.clear();
                }
                runs += 1;
            });
        });

        try {
            flushSync();
            expect(runs).toBe(1);
        } finally {
            dispose();
        }
    });

    it('does not let stale work advance or clear a newer tour', () => {
        const first = productTourCheckpoint.start(checkpoint.tourName, checkpoint.checkpointName, checkpoint.userId, checkpoint.organizationId);
        const second = productTourCheckpoint.start(checkpoint.tourName, checkpoint.checkpointName, checkpoint.userId, checkpoint.organizationId);

        expect(productTourCheckpoint.advance(first, 'command-search')).toBeUndefined();
        expect(productTourCheckpoint.clear(first)).toBe(false);
        expect(productTourCheckpoint.current).toBe(second);
    });
});
