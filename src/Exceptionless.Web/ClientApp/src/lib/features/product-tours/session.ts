import type { ProductTourCheckpoint, ProductTourLaunchSource, ProductTourName, ProductTourPhase } from './types';

import { PRODUCT_TOUR_CHECKPOINTS, PRODUCT_TOUR_LAUNCH_SOURCES } from './types';

const SESSION_KEY = 'exceptionless.product-tour';
const SOURCES = new Set<string>(PRODUCT_TOUR_LAUNCH_SOURCES);

export function clearProductTourSession(storage: Pick<Storage, 'removeItem'> = sessionStorage): void {
    storage.removeItem(SESSION_KEY);
}

export function readProductTourSession(storage: Pick<Storage, 'getItem' | 'removeItem'> = sessionStorage): ProductTourCheckpoint | undefined {
    try {
        const value = storage.getItem(SESSION_KEY);
        if (!value) return undefined;

        const candidate: unknown = JSON.parse(value);
        if (!isProductTourCheckpoint(candidate)) {
            clearProductTourSession(storage);
            return undefined;
        }

        return candidate;
    } catch {
        clearProductTourSession(storage);
        return undefined;
    }
}

export function writeProductTourSession(checkpoint: ProductTourCheckpoint, storage: Pick<Storage, 'setItem'> = sessionStorage): void {
    storage.setItem(SESSION_KEY, JSON.stringify(checkpoint));
}

function isPhase(value: unknown, tourName: string, checkpointName: unknown): value is ProductTourPhase {
    if (!isRecord(value) || typeof value.type !== 'string') return false;
    if (value.type === 'active') return true;
    return (
        tourName === 'saved-view-create' &&
        checkpointName === 'view-created' &&
        (value.type === 'saved-view-created' || value.type === 'saved-view-loaded') &&
        typeof value.viewId === 'string' &&
        !!value.viewId
    );
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
    if (typeof value.checkpointName !== 'string' || !checkpoints.includes(value.checkpointName)) return false;
    return isPhase(value.phase, value.tourName, value.checkpointName);
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
