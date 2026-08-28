import type { ProductTourCheckpoint, ProductTourCheckpointName, ProductTourPhase } from './types';

import { clearProductTourSession, readProductTourSession, writeProductTourSession } from './session';

class ProductTourCheckpointStore {
    current = $state.raw<ProductTourCheckpoint>();

    advance(
        expected: ProductTourCheckpoint,
        checkpointName: ProductTourCheckpointName,
        phase: ProductTourPhase = {
            type: 'active'
        },
        organizationId = expected.organizationId
    ) {
        if (this.current !== expected) {
            return undefined;
        }
        return this.save({
            ...expected,
            checkpointName,
            organizationId,
            phase
        });
    }

    clear(expected?: ProductTourCheckpoint): boolean {
        if (expected && this.current !== expected) {
            return false;
        }
        this.current = undefined;
        clearProductTourSession();
        return true;
    }

    restore(userId: string, organizationId?: string): ProductTourCheckpoint | undefined {
        if (this.current) {
            return this.current;
        }

        const stored = readProductTourSession();
        if (!stored) {
            return undefined;
        }

        if (stored.userId !== userId || stored.organizationId !== organizationId) {
            clearProductTourSession();
            return undefined;
        }

        this.current = stored;
        return stored;
    }

    start(checkpoint: ProductTourCheckpoint): ProductTourCheckpoint {
        return this.save(checkpoint);
    }

    private save(checkpoint: ProductTourCheckpoint): ProductTourCheckpoint {
        this.current = checkpoint;
        writeProductTourSession(checkpoint);
        return checkpoint;
    }
}

export const productTourCheckpoint = new ProductTourCheckpointStore();
