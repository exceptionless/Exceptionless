import { submitFeatureUsage } from '$features/auth/exceptionless-session';

import type { ProductTourKey } from './models';

export async function submitProductTourActivity(
    action: 'completed' | 'dismissed' | 'shown' | 'started',
    name: ProductTourKey,
    ..._legacy: unknown[]
): Promise<void> {
    void _legacy;
    if (action !== 'completed' && action !== 'dismissed') {
        return;
    }
    try {
        await submitFeatureUsage(`product-tour.${action}.${name}`);
    } catch {
        // Telemetry must not prevent navigation or saving functional guide progress.
    }
}
