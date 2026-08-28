import type { ProductTourKey, ProductTourLaunchSource } from './types';

export type ProductTourTelemetryEvent = 'completed' | 'dismissed' | 'shown' | 'started';

export function buildProductTourTelemetryEvent(
    event: ProductTourTelemetryEvent,
    name: ProductTourKey,
    version: number,
    source: ProductTourLaunchSource
): string {
    if (!Number.isSafeInteger(version) || version < 1) {
        throw new Error('Product tour telemetry requires a positive version.');
    }

    return ['product-tour', event, name, `v${version}`, source].join('.');
}
