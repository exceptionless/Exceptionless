import type { ProductTourKey, ProductTourLaunchSource } from './types';

export type ProductTourTelemetryEvent =
    | 'announcement-dismissed'
    | 'announcement-shown'
    | 'announcement-started'
    | 'chooser-shown'
    | 'chooser-skipped'
    | 'chooser-started'
    | 'completed'
    | 'dismissed'
    | 'failed'
    | 'started'
    | 'step';

const SAFE_SEGMENT = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export function buildProductTourTelemetryEvent(
    event: ProductTourTelemetryEvent,
    id: ProductTourKey,
    version: number,
    source: ProductTourLaunchSource,
    stepId?: string
): string {
    if (!SAFE_SEGMENT.test(event) || !SAFE_SEGMENT.test(id) || !SAFE_SEGMENT.test(source) || (stepId && !SAFE_SEGMENT.test(stepId))) {
        throw new Error('Product tour telemetry accepts stable catalog identifiers only.');
    }

    if (!Number.isSafeInteger(version) || version < 1) {
        throw new Error('Product tour telemetry requires a positive version.');
    }

    return ['product-tour', event, id, `v${version}`, source, stepId].filter(Boolean).join('.');
}
