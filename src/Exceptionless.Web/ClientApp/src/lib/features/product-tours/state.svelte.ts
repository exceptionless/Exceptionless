import type { ProductTourId } from './types';

class ProductTourRuntimeState {
    activeStepId = $state<string>();
    activeTourId = $state<ProductTourId>();

    clear(): void {
        this.activeStepId = undefined;
        this.activeTourId = undefined;
    }

    isActive(tourId: ProductTourId): boolean {
        return this.activeTourId === tourId;
    }

    set(tourId: ProductTourId, stepId?: string): void {
        this.activeTourId = tourId;
        this.activeStepId = stepId;
    }
}

export const productTourRuntime = new ProductTourRuntimeState();
