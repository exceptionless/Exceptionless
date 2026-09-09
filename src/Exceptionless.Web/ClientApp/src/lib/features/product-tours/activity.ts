import { submitFeatureUsage } from '$features/auth/exceptionless-session';

import type { ProductTourKey } from './models';

export async function submitProductTourActivity(action: 'completed' | 'dismissed', name: ProductTourKey): Promise<void> {
    try {
        await submitFeatureUsage(`product-tour.${action}.${name}`);
    } catch {
        // Telemetry must not prevent navigation or saving functional guide progress.
    }
}
