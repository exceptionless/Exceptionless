import type { ProductTourId, ProductTourLaunchSource } from './types';

export interface StoredProductTourSession {
    source: ProductTourLaunchSource;
    stepId?: string;
    tourId: ProductTourId;
    version: number;
}

const SESSION_KEY = 'exceptionless.product-tour';

export function clearProductTourSession(storage: Pick<Storage, 'removeItem'> = sessionStorage): void {
    storage.removeItem(SESSION_KEY);
}

export function readProductTourSession(storage: Pick<Storage, 'getItem' | 'removeItem'> = sessionStorage): StoredProductTourSession | undefined {
    try {
        const value = storage.getItem(SESSION_KEY);
        return value ? (JSON.parse(value) as StoredProductTourSession) : undefined;
    } catch {
        clearProductTourSession(storage);
        return undefined;
    }
}

export function writeProductTourSession(session: StoredProductTourSession, storage: Pick<Storage, 'setItem'> = sessionStorage): void {
    storage.setItem(SESSION_KEY, JSON.stringify(session));
}
