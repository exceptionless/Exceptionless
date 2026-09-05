import { submitFeatureUsage } from '$features/auth/exceptionless-session';

import type { ProductTourKey, ProductTourLaunchSource } from './models';

export async function submitProductTourActivity(
    action: 'completed' | 'dismissed' | 'shown' | 'started',
    name: ProductTourKey,
    version: number,
    source: ProductTourLaunchSource
): Promise<void> {
    try {
        await submitFeatureUsage(`product-tour.${action}.${name}.v${version}.${source}`);
    } catch {
        // Telemetry must not prevent navigation or saving functional guide progress.
    }
}
