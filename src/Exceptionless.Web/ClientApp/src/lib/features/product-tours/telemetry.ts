import type { ProductTourTelemetryEvent as ProductTourTelemetryEventContract } from '$generated/api';

import type { ProductTourKey, ProductTourLaunchSource } from './types';

export type ProductTourTelemetryEvent = `${ProductTourTelemetryEventContract}`;

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
