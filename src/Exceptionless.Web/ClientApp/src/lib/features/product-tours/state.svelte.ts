import type { ProductTourId, ProductTourLaunchSource } from './types';

export type ProductTourHostEvent =
    | { eventType?: string; type: 'event-opened' }
    | { stepId: string; tourId: ProductTourId; type: 'advance' }
    | { tourId: ProductTourId; type: 'completed' | 'dismissed' };

type ProductTourHostListener = (event: ProductTourHostEvent) => void;

class ProductTourHost {
    get activeStepId(): string | undefined {
        return this.session?.stepId;
    }
    get activeTourId(): ProductTourId | undefined {
        return this.session?.tourId;
    }

    get organizationId(): string | undefined {
        return this.session?.organizationId;
    }

    get source(): ProductTourLaunchSource | undefined {
        return this.session?.source;
    }

    private readonly listeners = new Set<ProductTourHostListener>();

    private session = $state<{
        organizationId?: string;
        source: ProductTourLaunchSource;
        stepId?: string;
        tourId: ProductTourId;
    }>();

    advance(tourId: ProductTourId, stepId: string): void {
        this.publish({
            stepId,
            tourId,
            type: 'advance'
        });
    }

    clear(): void {
        this.session = undefined;
    }

    complete(tourId: ProductTourId): void {
        this.publish({
            tourId,
            type: 'completed'
        });
    }

    dismiss(tourId: ProductTourId): void {
        this.publish({
            tourId,
            type: 'dismissed'
        });
    }

    eventOpened(eventType?: string): void {
        this.publish({
            eventType,
            type: 'event-opened'
        });
    }

    isActive(tourId: ProductTourId): boolean {
        return this.activeTourId === tourId;
    }

    set(tourId: ProductTourId, stepId: string | undefined, source?: ProductTourLaunchSource, organizationId?: string): void {
        const activeSource = source ?? this.session?.source;
        if (!activeSource) {
            throw new Error('A product tour session requires a launch source.');
        }

        this.session = {
            organizationId: organizationId ?? this.session?.organizationId,
            source: activeSource,
            stepId,
            tourId
        };
    }

    subscribe(listener: ProductTourHostListener): () => void {
        this.listeners.add(listener);
        return () => this.listeners.delete(listener);
    }

    private publish(event: ProductTourHostEvent): void {
        for (const listener of this.listeners) {
            listener(event);
        }
    }
}

export const productTourHost = new ProductTourHost();
