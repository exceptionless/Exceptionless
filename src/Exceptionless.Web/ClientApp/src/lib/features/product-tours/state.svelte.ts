import type { ProductTourCheckpoint, ProductTourCheckpointName, ProductTourLaunchSource, ProductTourName, ProductTourPhase } from './types';

import { clearProductTourSession, readProductTourSession, writeProductTourSession } from './session';

class ProductTourCheckpointStore {
    current = $state.raw<ProductTourCheckpoint>();

    advance<Name extends ProductTourCheckpoint['tourName']>(
        expected: ProductTourCheckpoint<Name>,
        checkpointName: ProductTourCheckpointName<Name>,
        phase: ProductTourPhase<Name> = {
            type: 'active'
        },
        organizationId = expected.organizationId
    ): ProductTourCheckpoint<Name> | undefined {
        if (this.current !== expected) {
            return undefined;
        }
        const next = {
            ...expected,
            checkpointName,
            organizationId,
            phase
        } as ProductTourCheckpoint<Name>;
        return this.save(next);
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

    start<Name extends ProductTourName>(
        tourName: Name,
        checkpointName: ProductTourCheckpointName<Name>,
        source: ProductTourLaunchSource,
        userId: string,
        version: number,
        organizationId?: string
    ): ProductTourCheckpoint<Name> {
        const checkpoint = {
            checkpointName,
            organizationId,
            phase: {
                type: 'active'
            },
            source,
            tourName,
            userId,
            version
        } as ProductTourCheckpoint<Name>;
        return this.save(checkpoint);
    }

    private save<Name extends ProductTourCheckpoint['tourName']>(checkpoint: ProductTourCheckpoint<Name>): ProductTourCheckpoint<Name> {
        this.current = checkpoint;
        writeProductTourSession(checkpoint);
        return checkpoint;
    }
}

export const productTourCheckpoint = new ProductTourCheckpointStore();
