import type { ProductTourCheckpoint, ProductTourCheckpointName, ProductTourName } from './models';

class ProductTourCheckpointStore {
    public current = $state.raw<ProductTourCheckpoint>();

    public advance<Name extends ProductTourCheckpoint['tourName']>(
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

    public clear(expected?: ProductTourCheckpoint): boolean {
        if (expected && this.current !== expected) {
            return false;
        }

        this.current = undefined;
        return true;
    }

    public start<Name extends ProductTourName>(
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

// The shell host pauses all spotlights while navigation or the catalog takes focus.
export const productTourPresentation = $state({
    suspended: false
});
