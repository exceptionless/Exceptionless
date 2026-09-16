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

        // Act
        const second = productTourCheckpoint.advance(first, 'command-search')!;

        // Assert
        expect(productTourCheckpoint.current).toBe(second);

        // Act
        const back = productTourCheckpoint.advance(second, 'navigation')!;

        // Assert
        expect(back.checkpointName).toBe('navigation');

        // Act
        const replay = productTourCheckpoint.start('app-overview', 'navigation', 'user');

        // Assert
        expect(productTourCheckpoint.current).toBe(replay);
    });

    it('does not retrigger an effect that clears an empty store', () => {
        // Arrange
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
            // Act
            flushSync();

            // Assert
            expect(runs).toBe(1);
        } finally {
            dispose();
        }
    });

    it('does not let stale work advance or clear a newer tour', () => {
        // Arrange
        const first = productTourCheckpoint.start(checkpoint.tourName, checkpoint.checkpointName, checkpoint.userId, checkpoint.organizationId);
        const second = productTourCheckpoint.start(checkpoint.tourName, checkpoint.checkpointName, checkpoint.userId, checkpoint.organizationId);

        // Act
        const advanced = productTourCheckpoint.advance(first, 'command-search');
        const cleared = productTourCheckpoint.clear(first);

        // Assert
        expect(advanced).toBeUndefined();
        expect(cleared).toBe(false);
        expect(productTourCheckpoint.current).toBe(second);
    });
});
