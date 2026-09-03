import type { ProductTourCheckpoint, ProductTourLaunchSource, ProductTourName } from './types';

import { PRODUCT_TOUR_CHECKPOINTS, PRODUCT_TOUR_LAUNCH_SOURCES } from './types';

const SESSION_KEY = 'exceptionless.product-tour';
const SOURCES = new Set<string>(PRODUCT_TOUR_LAUNCH_SOURCES);

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
        if (!value) return undefined;

        const candidate: unknown = JSON.parse(value);
        if (!isProductTourCheckpoint(candidate)) {
            clearProductTourSession(storage);
            return undefined;
        }

        return {
            checkpointName: candidate.checkpointName,
            ...(candidate.reachedSteps ? { reachedSteps: candidate.reachedSteps } : {}),
            organizationId: candidate.organizationId,
            source: candidate.source,
            tourName: candidate.tourName,
            userId: candidate.userId,
            version: candidate.version
        } as ProductTourCheckpoint;
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

function isProductTourCheckpoint(value: unknown): value is ProductTourCheckpoint {
    if (
        !isRecord(value) ||
        typeof value.userId !== 'string' ||
        !value.userId ||
        typeof value.tourName !== 'string' ||
        typeof value.version !== 'number' ||
        !Number.isSafeInteger(value.version) ||
        value.version < 1
    )
        return false;
    if (value.organizationId !== undefined && typeof value.organizationId !== 'string') return false;
    if (!isProductTourLaunchSource(value.source) || !isProductTourName(value.tourName)) return false;

    const checkpoints: readonly string[] = PRODUCT_TOUR_CHECKPOINTS[value.tourName];
    if (
        value.reachedSteps !== undefined &&
        (!Array.isArray(value.reachedSteps) ||
            value.reachedSteps.length > checkpoints.length ||
            !value.reachedSteps.every((step) => typeof step === 'string' && checkpoints.includes(step)))
    ) {
        return false;
    }
    if (typeof value.checkpointName !== 'string' || !checkpoints.includes(value.checkpointName)) return false;
    return true;
}

function isProductTourLaunchSource(value: unknown): value is ProductTourLaunchSource {
    return typeof value === 'string' && SOURCES.has(value);
}

function isProductTourName(value: string): value is ProductTourName {
    return Object.hasOwn(PRODUCT_TOUR_CHECKPOINTS, value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}
