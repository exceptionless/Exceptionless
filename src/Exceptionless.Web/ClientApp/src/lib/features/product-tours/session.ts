import { number, object, string, enum as zodEnum } from 'zod';

import type { ProductTourCheckpoint, ProductTourName } from './models';

import { PRODUCT_TOUR_CHECKPOINTS, PRODUCT_TOUR_LAUNCH_SOURCES } from './models';

const SESSION_KEY = 'exceptionless.product-tour';
const checkpointSchema = object({
    checkpointName: string(),
    organizationId: string().optional(),
    source: zodEnum(PRODUCT_TOUR_LAUNCH_SOURCES),
    tourName: zodEnum(Object.keys(PRODUCT_TOUR_CHECKPOINTS) as ProductTourName[]),
    userId: string().min(1),
    version: number().int().positive()
}).refine((value) => {
    const checkpoints: readonly string[] = PRODUCT_TOUR_CHECKPOINTS[value.tourName];
    return checkpoints.includes(value.checkpointName);
});

export function clearProductTourSession(storage?: Pick<Storage, 'removeItem'>): void {
    try {
        (storage ?? sessionStorage).removeItem(SESSION_KEY);
    } catch {
        // The guide can still run in memory when browser storage is unavailable.
    }
}

export function readProductTourSession(storage?: Pick<Storage, 'getItem' | 'removeItem'>): ProductTourCheckpoint | undefined {
    try {
        const value = (storage ?? sessionStorage).getItem(SESSION_KEY);
        if (!value) {
            return undefined;
        }

        const candidate = checkpointSchema.safeParse(JSON.parse(value));
        if (!candidate.success) {
            clearProductTourSession(storage);
            return undefined;
        }

        return candidate.data as ProductTourCheckpoint;
    } catch {
        clearProductTourSession(storage);
        return undefined;
    }
}

export function writeProductTourSession(checkpoint: ProductTourCheckpoint, storage?: Pick<Storage, 'setItem'>): void {
    try {
        (storage ?? sessionStorage).setItem(SESSION_KEY, JSON.stringify(checkpoint));
    } catch {
        // Persistence is best effort; the in-memory checkpoint remains usable.
    }
}
