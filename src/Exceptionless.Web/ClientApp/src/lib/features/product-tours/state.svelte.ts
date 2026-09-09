import type { ProductTourCheckpoint, ProductTourCheckpointName, ProductTourName } from './models';

class ProductTourCheckpointStore {
    current = $state.raw<ProductTourCheckpoint>();

    advance<Name extends ProductTourCheckpoint['tourName']>(
        expected: ProductTourCheckpoint<Name>,
        checkpointName: ProductTourCheckpointName<Name>,
        organizationId = expected.organizationId
    ): ProductTourCheckpoint<Name> | undefined {
        if (this.current !== expected) {
            return undefined;
        }
        const next = {
            ...expected,
            checkpointName,
            organizationId
        } as ProductTourCheckpoint<Name>;
        this.current = next;
        return next;
    }

    clear(expected?: ProductTourCheckpoint): boolean {
        if (expected && this.current !== expected) {
            return false;
        }

        this.current = undefined;
        return true;
    }

    start<Name extends ProductTourName>(
        tourName: Name,
        checkpointName: ProductTourCheckpointName<Name>,
        userId: string,
        organizationId?: string
    ): ProductTourCheckpoint<Name> {
        const checkpoint = {
            checkpointName,
            organizationId,
            tourName,
            userId
        } as ProductTourCheckpoint<Name>;
        this.current = checkpoint;
        return checkpoint;
    }
}

export const productTourCheckpoint = new ProductTourCheckpointStore();
